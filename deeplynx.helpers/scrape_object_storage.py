import logging
import requests
import urllib3
import warnings

urllib3.disable_warnings(urllib3.exceptions.NotOpenSSLWarning)
warnings.filterwarnings('ignore')
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# ---- Configuration ----------------------------------------------------
# For local dev against Docker: the API's local-bypass auth path doesn't
# validate this token at all, so BASE_URL below should point at your local
# instance and TOKEN can stay as a placeholder. Before running against any
# real (non-local) environment, set BASE_URL to that environment's URL and
# TOKEN to a real bearer token for a user with is_sys_admin = true.
BASE_URL = "http://localhost:5000/api/v1/maintenance/object-storages"
TOKEN = "MY.NEXUS.TOKEN"  # ignored by local dev auth bypass; required for real environments
BATCH_SIZE = 500
MAX_BATCHES = 5

# Each entry is one object storage to scrape, and the data source under
# which its records should be created.
# TODO: fill in with the real object storage / data source IDs you want to run.
SCRAPE_TARGETS = [
    {"object_storage_id": 20, "data_source_id": 14},
]

# Optional sensitivity labels applied to every created record.
# Leave empty to apply no labels.
SENSITIVITY_LABEL_IDS = []

LOG_EVERY = 1000  # log a running-total checkpoint every N records processed

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

        log.info(
            "Sending scrape request: cursor=%s",
            after_cursor if after_cursor is not None else "start",
        )

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
            response.raise_for_status()

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