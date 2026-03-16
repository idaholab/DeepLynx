"use client";

import React, { useEffect, useRef, useState } from "react";
import {
  fetchInsightIngestionStatus,
  queueInsightUpload,
  streamInsightQuery,
} from "@/app/lib/client_service/insight_services.client";
import { useLanguage } from "@/app/contexts/Language";
import {
  ArrowsPointingInIcon,
  ArrowsPointingOutIcon,
  ChatBubbleLeftRightIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  CloudArrowUpIcon,
  PaperAirplaneIcon,
} from "@heroicons/react/24/outline";

type InsightRole = "assistant" | "user";
type IngestionState =
  | "not_queued"
  | "queued"
  | "processing"
  | "ready"
  | "error";

interface InsightMessage {
  id: number;
  role: InsightRole;
  content: string;
  timestamp: string;
}

interface RecordInsightChatProps {
  recordId?: number;
  recordUri?: string | null;
  recordName?: string | null;
}

const STATUS_POLL_INTERVAL_MS = 5000;
const INGESTION_BADGE_CLASS: Record<IngestionState, string> = {
  not_queued: "badge-ghost",
  queued: "badge-warning",
  processing: "badge-warning",
  ready: "badge-success",
  error: "badge-error",
};

function getCurrentTimestamp(): string {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date());
}

function playAudio(audioRef: React.RefObject<HTMLAudioElement | null>) {
  const audio = audioRef.current;
  if (!audio) return;

  audio.currentTime = 0;
  const playPromise = audio.play();
  if (playPromise) {
    void playPromise.catch(() => {});
  }
}

function withRecordName(template: string, recordName: string): string {
  return template.replace("{recordName}", recordName);
}

function buildIntroMessage(
  recordName: string,
  state: IngestionState,
  readyTemplate: string,
  notReadyTemplate: string,
): string {
  return state === "ready"
    ? withRecordName(readyTemplate, recordName)
    : withRecordName(notReadyTemplate, recordName);
}

const RecordInsightChat: React.FC<RecordInsightChatProps> = ({
  recordId,
  recordUri,
  recordName,
}) => {
  const { t } = useLanguage();
  const trimmedRecordName = recordName?.trim() ?? "";
  const safeRecordName =
    trimmedRecordName || t.translations.INSIGHT_THIS_RECORD;

  const messageIdRef = useRef(1);
  const scrollAnchorRef = useRef<HTMLDivElement>(null);
  const promptInputRef = useRef<HTMLInputElement>(null);
  const readyAudioRef = useRef<HTMLAudioElement | null>(null);
  const responseAudioRef = useRef<HTMLAudioElement | null>(null);
  const previousIngestionStateRef = useRef<IngestionState>("not_queued");
  const previousIsRespondingRef = useRef(false);
  const [draft, setDraft] = useState("");
  const [isResponding, setIsResponding] = useState(false);
  const [isQueueingUpload, setIsQueueingUpload] = useState(false);
  const [isWidgetCollapsed, setIsWidgetCollapsed] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);
  const [ingestionState, setIngestionState] =
    useState<IngestionState>("not_queued");
  const [messages, setMessages] = useState<InsightMessage[]>([]);

  function createMessage(role: InsightRole, content: string): InsightMessage {
    return {
      id: messageIdRef.current++,
      role,
      content,
      timestamp: getCurrentTimestamp(),
    };
  }

  function appendMessageChunk(messageId: number, chunk: string) {
    setMessages((prev) =>
      prev.map((message) =>
        message.id === messageId
          ? { ...message, content: `${message.content}${chunk}` }
          : message,
      ),
    );
  }

  function replaceMessageContent(messageId: number, content: string) {
    setMessages((prev) =>
      prev.map((message) =>
        message.id === messageId ? { ...message, content } : message,
      ),
    );
  }

  useEffect(() => {
    messageIdRef.current = 1;
    setMessages([
      {
        id: messageIdRef.current++,
        role: "assistant",
        content: buildIntroMessage(
          safeRecordName,
          "not_queued",
          t.translations.INSIGHT_INTRO_READY,
          t.translations.INSIGHT_INTRO_NOT_READY,
        ),
        timestamp: getCurrentTimestamp(),
      },
    ]);
    setDraft("");
    setIsResponding(false);
    setIsQueueingUpload(false);
    setIsWidgetCollapsed(false);
    setIsExpanded(false);
    setIngestionState("not_queued");
    previousIngestionStateRef.current = "not_queued";
  }, [
    safeRecordName,
    recordId,
    t.translations.INSIGHT_INTRO_READY,
    t.translations.INSIGHT_INTRO_NOT_READY,
  ]);

  useEffect(() => {
    setMessages((prev) => {
      if (prev.length !== 1 || prev[0]?.role !== "assistant") {
        return prev;
      }

      return [
        {
          ...prev[0],
          content: buildIntroMessage(
            safeRecordName,
            ingestionState,
            t.translations.INSIGHT_INTRO_READY,
            t.translations.INSIGHT_INTRO_NOT_READY,
          ),
        },
      ];
    });
  }, [
    ingestionState,
    safeRecordName,
    t.translations.INSIGHT_INTRO_READY,
    t.translations.INSIGHT_INTRO_NOT_READY,
  ]);

  useEffect(() => {
    scrollAnchorRef.current?.scrollIntoView({
      behavior: "smooth",
      block: "end",
    });
  }, [messages, isResponding]);

  useEffect(() => {
    const justFinishedResponding =
      previousIsRespondingRef.current && !isResponding;
    if (justFinishedResponding && !isWidgetCollapsed) {
      promptInputRef.current?.focus();
    }
    previousIsRespondingRef.current = isResponding;
  }, [isResponding, isWidgetCollapsed]);

  useEffect(() => {
    readyAudioRef.current = new Audio("/assets/notification.mp3");
    readyAudioRef.current.preload = "auto";

    responseAudioRef.current = new Audio("/assets/pop.mp3");
    responseAudioRef.current.preload = "auto";

    return () => {
      if (readyAudioRef.current) {
        readyAudioRef.current.pause();
        readyAudioRef.current = null;
      }
      if (responseAudioRef.current) {
        responseAudioRef.current.pause();
        responseAudioRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    const previousState = previousIngestionStateRef.current;
    if (
      ingestionState === "ready" &&
      (previousState === "queued" || previousState === "processing")
    ) {
      playAudio(readyAudioRef);
    }
    previousIngestionStateRef.current = ingestionState;
  }, [ingestionState]);

  useEffect(() => {
    if (!recordId) return;

    let cancelled = false;

    const checkInitialStatus = async () => {
      try {
        const status = await fetchInsightIngestionStatus(recordId);
        if (cancelled || !status.indexed) return;

        setIngestionState("ready");
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
          return;
        }

        setIngestionState((prev) => (prev === "queued" ? "processing" : prev));
      } catch (error) {
        if (cancelled) return;
        console.error("Insight status check failed during polling:", error);
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

  async function handleSend(input: string) {
    const prompt = input.trim();
    if (!prompt || isResponding) return;
    if (ingestionState !== "ready") {
      setMessages((prev) => [
        ...prev,
        createMessage("assistant", t.translations.INSIGHT_NOT_READY_MESSAGE),
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
          t.translations.INSIGHT_EMPTY_RESPONSE,
        );
      }
      playAudio(responseAudioRef);
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : t.translations.INSIGHT_UNKNOWN_ERROR;
      replaceMessageContent(
        assistantMessage.id,
        `${t.translations.INSIGHT_ERROR_PREFIX}: ${message}`,
      );
    } finally {
      setIsResponding(false);
    }
  }

  async function handleQueueUpload() {
    if (!recordId) {
      setIngestionState("error");
      return;
    }

    const uri = recordUri?.trim();
    if (!uri) {
      setIngestionState("error");
      return;
    }

    setIsQueueingUpload(true);
    setIngestionState("queued");

    try {
      const result = await queueInsightUpload({
        fileInfo: [{ fileId: recordId, fileURI: uri }],
      });
      const first = result.results[0];

      if (!first) {
        setIngestionState("error");
        return;
      }

      if (first.status === "queued") {
        setIngestionState("queued");
      } else {
        setIngestionState("error");
      }
    } catch (error) {
      console.error("Insight upload failed:", error);
      setIngestionState("error");
    } finally {
      setIsQueueingUpload(false);
    }
  }

  const ingestionBadgeLabel =
    ingestionState === "ready"
      ? t.translations.READY
      : ingestionState === "queued"
        ? t.translations.INSIGHT_STATUS_QUEUED
        : ingestionState === "processing"
          ? t.translations.INSIGHT_STATUS_PROCESSING
          : ingestionState === "error"
            ? t.translations.INSIGHT_STATUS_ERROR
            : t.translations.INSIGHT_STATUS_NOT_QUEUED;

  const introMessage = buildIntroMessage(
    safeRecordName,
    ingestionState,
    t.translations.INSIGHT_INTRO_READY,
    t.translations.INSIGHT_INTRO_NOT_READY,
  );
  const hasStartedConversation = messages.length > 1 || isResponding;
  const visibleMessages = hasStartedConversation ? messages.slice(1) : [];

  return (
    <div className="card bg-base-100 shadow-md mt-4 p-2">
      <div className="flex items-center justify-between gap-3 px-4 py-1">
        <div className="flex min-w-0 items-center gap-2">
          <ChatBubbleLeftRightIcon className="size-6 text-secondary shrink-0" />
          <h3 className="text-xl font-bold text-base-content">
            {t.translations.INSIGHT}
          </h3>
          <span className="badge badge-outline badge-sm">
            {t.translations.INSIGHT_FILE_SCOPED}
          </span>
          <span
            className={`badge badge-sm ${INGESTION_BADGE_CLASS[ingestionState]}`}
          >
            {ingestionBadgeLabel}
          </span>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            className="btn btn-ghost btn-sm gap-2"
            onClick={() => {
              void handleQueueUpload();
            }}
            disabled={isQueueingUpload || isResponding}
          >
            <CloudArrowUpIcon className="size-5" />
            {isQueueingUpload
              ? t.translations.INSIGHT_QUEUEING
              : t.translations.INSIGHT_QUEUE_RECORD}
          </button>
          <button
            type="button"
            className="btn btn-ghost btn-xs btn-circle"
            onClick={() => setIsExpanded((prev) => !prev)}
            title={isExpanded ? "Collapse height" : "Expand height"}
            disabled={isWidgetCollapsed}
          >
            {isExpanded ? (
              <ArrowsPointingInIcon className="size-6" />
            ) : (
              <ArrowsPointingOutIcon className="size-6" />
            )}
          </button>
          <button
            type="button"
            className="btn btn-ghost btn-xs btn-circle"
            onClick={() => setIsWidgetCollapsed((prev) => !prev)}
            title={isWidgetCollapsed ? "Expand widget" : "Collapse widget"}
          >
            {isWidgetCollapsed ? (
              <ChevronDownIcon className="size-6" />
            ) : (
              <ChevronUpIcon className="size-6" />
            )}
          </button>
        </div>
      </div>

      {!isWidgetCollapsed && (
        <div
          className={`card-body p-4 pt-2 ${isExpanded ? "h-[34rem]" : "h-[24rem]"}`}
        >
          <div className="h-full min-h-0">
            <div className="flex h-full min-h-0 flex-col rounded-box border border-base-300 bg-base-100">
              <div className="flex-1 min-h-0 overflow-y-auto bg-base-200/30 p-3">
                {!hasStartedConversation ? (
                  <div className="flex h-full items-center justify-center">
                    <div className="max-w-xl text-base-content/80">
                      <p className="text-lg">{introMessage}</p>
                      <p className="mt-2 text-sm text-base-content/70">
                        {t.translations.INSIGHT_CONVERSATION_NOT_SAVED}
                      </p>
                    </div>
                  </div>
                ) : (
                  <div className="space-y-3">
                    {visibleMessages.map((message) => (
                      <div
                        key={message.id}
                        className={`chat ${
                          message.role === "user" ? "chat-end" : "chat-start"
                        }`}
                      >
                        <div className="chat-header text-xs text-base-content/60 mb-1">
                          {message.role === "user"
                            ? t.translations.INSIGHT_YOU
                            : t.translations.INSIGHT}
                          <time className="ml-2">{message.timestamp}</time>
                        </div>
                        <div
                          className={`chat-bubble whitespace-pre-wrap ${
                            message.role === "user"
                              ? "bg-primary text-primary-content"
                              : "border border-base-300/60 bg-base-100 text-base-content"
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
                )}
              </div>

              <div className="border-t border-base-300 p-3">
                <form
                  className="flex flex-col gap-2 lg:flex-row lg:items-center"
                  onSubmit={(e) => {
                    e.preventDefault();
                    const prompt = draft;
                    setDraft("");
                    void handleSend(prompt);
                  }}
                >
                  <input
                    ref={promptInputRef}
                    type="text"
                    className="input input-bordered w-full bg-base-100"
                    placeholder={t.translations.INSIGHT_ASK_PLACEHOLDER}
                    value={draft}
                    onChange={(e) => setDraft(e.target.value)}
                    disabled={isResponding}
                  />
                  <button
                    type="submit"
                    className="btn btn-primary btn-sm gap-2"
                    disabled={!draft.trim() || isResponding}
                    aria-label={t.translations.INSIGHT_SEND_PROMPT_ARIA}
                  >
                    <PaperAirplaneIcon className="size-5" />
                    {t.translations.INSIGHT_SEND}
                  </button>
                </form>

                <p className="mt-2 text-xs text-base-content/70">
                  {t.translations.INSIGHT_CONVERSATION_NOT_SAVED}
                </p>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default RecordInsightChat;
