import logging
import requests
import time
import urllib3
import warnings

urllib3.disable_warnings(urllib3.exceptions.NotOpenSSLWarning)
warnings.filterwarnings('ignore')
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# ---- Configuration ----------------------------------------------------
BASE_URL = "https://deeplynx.inl.gov/api/v1/maintenance/object-storages"
TOKEN = "MY.NEXUS.TOKEN"  
BATCH_SIZE = 500
MAX_BATCHES = 5
MAX_RETRIES = 30
RETRY_DELAY_SECONDS = 5
SCRAPE_TARGETS = [
    {"object_storage_id": 1, "data_source_id": 1},
]
SENSITIVITY_LABEL_IDS = []
LOG_EVERY = 1000  

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
log = logging.getLogger("scrape")


def run_scrape_for_storage(object_storage_id: int, data_source_id: int) -> None:
    after_cursor = None
    total_processed = 0

    log.info(
        "Starting scrape for objectStorageId=%s dataSourceId=%s",
        object_storage_id, data_source_id,
    )

    seen_cursors = set()

    while True:
        params = {
            "dataSourceId": str(data_source_id),
            "batchSize": str(BATCH_SIZE),
            "maxBatches": str(MAX_BATCHES),
        }

        if after_cursor is not None:
            params["afterCursor"] = after_cursor

        for label_id in SENSITIVITY_LABEL_IDS:
            params.setdefault("sensitivityLabelIds", []).append(str(label_id))

        cursor_display = (
            after_cursor
            if after_cursor is not None
            else "start"
        )

        log.info(
            "Sending scrape request: cursor=%s",
            after_cursor if after_cursor is not None else "start",
        )

        attempt = 0

        while True:
            try:
                response = requests.post(
                    f"{BASE_URL}/{object_storage_id}/scrape",
                    headers={
                        "Authorization": f"Bearer {TOKEN}"
                    } if TOKEN != "MY.NEXUS.TOKEN" else {},
                    params=params,
                    verify=False,
                    timeout=(10, 300),
                )

                if not response.ok:
                    log.error(
                        "Scrape failed with status %s: %s",
                        response.status_code,
                        response.text,
                    )

                    # HTTP errors such as 400 or 500 are not retried here.
                    response.raise_for_status()

                break

            except (
                requests.ConnectionError,
                requests.Timeout,
            ) as exc:
                attempt += 1

                if attempt >= MAX_RETRIES:
                    log.error(
                        "Request failed after %s attempts at cursor=%s: %s",
                        attempt,
                        cursor_display,
                        exc,
                    )
                    raise

                log.warning(
                    "Connection interrupted at cursor=%s: %s. "
                    "Retrying in %s seconds (%s/%s).",
                    cursor_display,
                    exc,
                    RETRY_DELAY_SECONDS,
                    attempt,
                    MAX_RETRIES,
                )

                time.sleep(RETRY_DELAY_SECONDS)

        data = response.json()

        processed = data.get("processed", 0)
        next_cursor = data.get("nextCursor")

        total_processed += processed

        log.info(
            "Processed=%s nextCursor=%s total=%s",
            processed,
            next_cursor,
            total_processed,
        )

        if next_cursor is None:
            break

        if next_cursor == after_cursor or next_cursor in seen_cursors:
            raise RuntimeError(
                f"Server returned a repeated cursor: {next_cursor}"
            )

        seen_cursors.add(next_cursor)
        after_cursor = next_cursor

    log.info(
        "Finished scrape for objectStorageId=%s | total_processed=%s",
        object_storage_id, total_processed,
    )


def main():
    for target in SCRAPE_TARGETS:
        run_scrape_for_storage(target["object_storage_id"], target["data_source_id"])


if __name__ == "__main__":
    main()