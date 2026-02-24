"use client";

import {
  PaperAirplaneIcon,
  SparklesIcon,
} from "@heroicons/react/24/outline";
import React, { useCallback, useEffect, useRef, useState } from "react";
import {
  fetchInsightIngestionStatus,
  queueInsightUpload,
  streamInsightQuery,
} from "@/app/lib/client_service/insight_services.client";

type InsightRole = "assistant" | "user";
type IngestionState = "not_queued" | "queued" | "processing" | "ready" | "error";

interface InsightMessage {
  id: number;
  role: InsightRole;
  content: string;
  timestamp: string;
}

interface InsightChatMockProps {
  recordId?: number;
  recordUri?: string | null;
  recordName?: string | null;
  recordClassName?: string | null;
  recordDescription?: string | null;
  dataSourceName?: string | null;
}

const QUICK_PROMPTS = [
  "Summarize this record for an engineer.",
  "What fields look incomplete?",
  "What should I validate next?",
];
const STATUS_POLL_INTERVAL_MS = 5000;

function getCurrentTimestamp(): string {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date());
}

function buildIntroMessage(recordName: string): string {
  return `I am ready to help analyze "${recordName}". First queue the record for Insight indexing, then send questions here.`;
}

const InsightChatMock: React.FC<InsightChatMockProps> = ({
  recordId,
  recordUri,
  recordName,
  recordClassName,
  recordDescription,
  dataSourceName,
}) => {
  const safeRecordName = recordName?.trim().length
    ? recordName
    : "this record";

  const messageIdRef = useRef(1);
  const scrollAnchorRef = useRef<HTMLDivElement>(null);
  const [draft, setDraft] = useState("");
  const [isResponding, setIsResponding] = useState(false);
  const [isQueueingUpload, setIsQueueingUpload] = useState(false);
  const [ingestionState, setIngestionState] = useState<IngestionState>(
    "not_queued",
  );
  const [ingestionStatus, setIngestionStatus] = useState<string>(
    "Not queued for Insight yet.",
  );
  const [messages, setMessages] = useState<InsightMessage[]>([]);

  const createMessage = useCallback(
    (role: InsightRole, content: string): InsightMessage => ({
      id: messageIdRef.current++,
      role,
      content,
      timestamp: getCurrentTimestamp(),
    }),
    [],
  );

  useEffect(() => {
    messageIdRef.current = 1;
    setMessages([
      {
        id: messageIdRef.current++,
        role: "assistant",
        content: buildIntroMessage(safeRecordName),
        timestamp: getCurrentTimestamp(),
      },
    ]);
    setDraft("");
    setIsResponding(false);
    setIsQueueingUpload(false);
    setIngestionState("not_queued");
    setIngestionStatus("Not queued for Insight yet.");
  }, [safeRecordName, recordId]);

  useEffect(() => {
    scrollAnchorRef.current?.scrollIntoView({
      behavior: "smooth",
      block: "end",
    });
  }, [messages, isResponding]);

  useEffect(() => {
    if (!recordId) return;

    let cancelled = false;

    const checkInitialStatus = async () => {
      try {
        const status = await fetchInsightIngestionStatus(recordId);
        if (cancelled || !status.indexed) return;

        setIngestionState("ready");
        setIngestionStatus(
          `Ready: ${status.chunk_count} chunks across ${status.page_count} pages.`,
        );
      } catch {
        // Keep default status text for initial load.
      }
    };

    void checkInitialStatus();

    return () => {
      cancelled = true;
    };
  }, [recordId]);

  useEffect(() => {
    if (!recordId) return;
    if (ingestionState !== "queued" && ingestionState !== "processing") return;

    let cancelled = false;

    const poll = async () => {
      try {
        const status = await fetchInsightIngestionStatus(recordId);
        if (cancelled) return;

        if (status.indexed) {
          setIngestionState("ready");
          setIngestionStatus(
            `Ready: ${status.chunk_count} chunks across ${status.page_count} pages.`,
          );
          return;
        }

        setIngestionState((prev) => (prev === "queued" ? "processing" : prev));
        setIngestionStatus((prev) =>
          prev.startsWith("Queued")
            ? "Queued. Processing in Insight..."
            : "Not indexed yet. Queue the record for Insight indexing.",
        );
      } catch (error) {
        if (cancelled) return;
        const message =
          error instanceof Error ? error.message : "Unknown status error";
        setIngestionStatus(`Status check failed, retrying: ${message}`);
      }
    };

    void poll();
    const interval = setInterval(() => {
      void poll();
    }, STATUS_POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [recordId, ingestionState]);

  const handleCheckStatus = useCallback(async () => {
    if (!recordId) return;

    try {
      const status = await fetchInsightIngestionStatus(recordId);
      if (status.indexed) {
        setIngestionState("ready");
        setIngestionStatus(
          `Ready: ${status.chunk_count} chunks across ${status.page_count} pages.`,
        );
        return;
      }

      if (ingestionState === "queued" || ingestionState === "processing") {
        setIngestionState("processing");
        setIngestionStatus("Queued. Processing in Insight...");
      } else {
        setIngestionState("not_queued");
        setIngestionStatus("Not indexed yet. Queue the record for Insight indexing.");
      }
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Unknown status error";
      setIngestionState("error");
      setIngestionStatus(`Status check failed: ${message}`);
    }
  }, [ingestionState, recordId]);

  const appendMessageChunk = useCallback((messageId: number, chunk: string) => {
    setMessages((prev) =>
      prev.map((message) =>
        message.id === messageId
          ? { ...message, content: `${message.content}${chunk}` }
          : message,
      ),
    );
  }, []);

  const replaceMessageContent = useCallback((messageId: number, content: string) => {
    setMessages((prev) =>
      prev.map((message) =>
        message.id === messageId ? { ...message, content } : message,
      ),
    );
  }, []);

  const handleSend = useCallback(
    async (input: string) => {
      const prompt = input.trim();
      if (!prompt || isResponding) return;
      if (ingestionState !== "ready") {
        setMessages((prev) => [
          ...prev,
          createMessage(
            "assistant",
            "This record is not ready yet. Queue it first and wait for status to become Ready.",
          ),
        ]);
        return;
      }

      const userMessage = createMessage("user", prompt);
      const assistantMessage = createMessage("assistant", "");

      setMessages((prev) => [...prev, userMessage, assistantMessage]);
      setIsResponding(true);

      try {
        const responseText = await streamInsightQuery(
          {
            question: prompt,
            fileIds: recordId ? [recordId] : undefined,
          },
          (chunk) => appendMessageChunk(assistantMessage.id, chunk),
        );

        if (!responseText.trim()) {
          replaceMessageContent(
            assistantMessage.id,
            "Insight returned an empty response.",
          );
        }
      } catch (error) {
        const message =
          error instanceof Error ? error.message : "Unknown Insight error";
        replaceMessageContent(assistantMessage.id, `Insight error: ${message}`);
      } finally {
        setIsResponding(false);
      }
    },
    [
      appendMessageChunk,
      createMessage,
      ingestionState,
      isResponding,
      recordId,
      replaceMessageContent,
    ],
  );

  const handleQueueUpload = useCallback(async () => {
    if (!recordId) {
      setIngestionState("error");
      setIngestionStatus("Cannot queue upload: missing record ID.");
      return;
    }

    const uri = recordUri?.trim();
    if (!uri) {
      setIngestionState("error");
      setIngestionStatus("Cannot queue upload: this record has no URI.");
      return;
    }

    setIsQueueingUpload(true);
    setIngestionState("queued");
    setIngestionStatus("Queueing upload...");

    try {
      const result = await queueInsightUpload({
        fileInfo: [{ fileId: recordId, fileURI: uri }],
      });
      const first = result.results[0];

      if (!first) {
        setIngestionState("error");
        setIngestionStatus("Upload request returned no result rows.");
        return;
      }

      if (first.status === "queued") {
        setIngestionState("queued");
        setIngestionStatus(
          "Queued successfully. Checking indexing status...",
        );
      } else {
        setIngestionState("error");
        setIngestionStatus(`Upload failed: ${first.error || "Unknown error"}`);
      }
    } catch (error) {
      const message =
        error instanceof Error ? error.message : "Unknown upload error";
      setIngestionState("error");
      setIngestionStatus(`Upload failed: ${message}`);
    } finally {
      setIsQueueingUpload(false);
    }
  }, [recordId, recordUri]);

  const ingestionBadgeClass =
    ingestionState === "ready"
      ? "badge-success"
      : ingestionState === "processing" || ingestionState === "queued"
        ? "badge-warning"
        : ingestionState === "error"
          ? "badge-error"
          : "badge-ghost";
  const ingestionBadgeLabel =
    ingestionState === "ready"
      ? "Ready"
      : ingestionState === "processing"
        ? "Processing"
        : ingestionState === "queued"
          ? "Queued"
          : ingestionState === "error"
            ? "Error"
            : "Not queued";

  return (
    <div className="card bg-base-100 shadow-lg">
      <div className="card-body p-4">
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <SparklesIcon className="size-5 text-primary" />
            <h3 className="font-semibold text-base-content">Insight</h3>
          </div>
          <span className="badge badge-outline badge-success badge-sm">
            Live
          </span>
        </div>

        <p className="text-xs text-base-content/60">
          Context: {recordClassName || "No class"} · {dataSourceName || "No source"} ·{" "}
          {recordDescription?.trim().length ? "Description present" : "No description"}
        </p>

        <div className="flex items-center gap-2">
          <span className={`badge badge-sm ${ingestionBadgeClass}`}>
            {ingestionBadgeLabel}
          </span>
          <button
            type="button"
            className="btn btn-xs btn-secondary"
            onClick={() => {
              void handleQueueUpload();
            }}
            disabled={isQueueingUpload || isResponding}
          >
            {isQueueingUpload ? "Queueing..." : "Queue Record For Insight"}
          </button>
          <button
            type="button"
            className="btn btn-xs btn-ghost"
            onClick={() => {
              void handleCheckStatus();
            }}
            disabled={isQueueingUpload || isResponding || !recordId}
          >
            Check Status
          </button>
          <span className="text-xs text-base-content/60">{ingestionStatus}</span>
        </div>

        <div className="rounded-box border border-base-300 bg-base-100 mt-2">
          <div className="h-72 overflow-y-auto px-3 py-3 space-y-3">
            {messages.map((message) => (
              <div
                key={message.id}
                className={`chat ${message.role === "user" ? "chat-end" : "chat-start"}`}
              >
                <div className="chat-header text-xs text-base-content/60 mb-1">
                  {message.role === "user" ? "You" : "Insight"}
                  <time className="ml-2">{message.timestamp}</time>
                </div>
                <div
                  className={`chat-bubble whitespace-pre-wrap ${
                    message.role === "user"
                      ? "chat-bubble-primary"
                      : "chat-bubble-neutral"
                  }`}
                >
                  {message.content || (
                    <span className="loading loading-dots loading-sm" />
                  )}
                </div>
              </div>
            ))}
            <div ref={scrollAnchorRef} />
          </div>
        </div>

        <div className="flex flex-wrap gap-2 mt-3">
          {QUICK_PROMPTS.map((prompt) => (
            <button
              key={prompt}
              type="button"
              className="btn btn-xs btn-outline"
              onClick={() => {
                void handleSend(prompt);
              }}
              disabled={isResponding}
            >
              {prompt}
            </button>
          ))}
        </div>

        <form
          className="flex items-center gap-2 mt-2"
          onSubmit={(e) => {
            e.preventDefault();
            const prompt = draft;
            setDraft("");
            void handleSend(prompt);
          }}
        >
          <input
            type="text"
            className="input input-bordered w-full"
            placeholder="Ask Insight about this record..."
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            disabled={isResponding}
          />
          <button
            type="submit"
            className="btn btn-primary btn-square"
            disabled={!draft.trim() || isResponding}
            aria-label="Send insight prompt"
          >
            <PaperAirplaneIcon className="size-4" />
          </button>
        </form>
      </div>
    </div>
  );
};

export default InsightChatMock;
