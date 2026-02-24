"use client";

import {
  PaperAirplaneIcon,
  SparklesIcon,
} from "@heroicons/react/24/outline";
import React, { useCallback, useEffect, useRef, useState } from "react";
import { streamInsightQuery } from "@/app/lib/client_service/insight_services.client";

type InsightRole = "assistant" | "user";

interface InsightMessage {
  id: number;
  role: InsightRole;
  content: string;
  timestamp: string;
}

interface InsightChatMockProps {
  recordId?: number;
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

function getCurrentTimestamp(): string {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date());
}

function buildIntroMessage(recordName: string): string {
  return `I am ready to help analyze "${recordName}". Responses stream from the Insight service through a local UI proxy route.`;
}

const InsightChatMock: React.FC<InsightChatMockProps> = ({
  recordId,
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
  }, [safeRecordName, recordId]);

  useEffect(() => {
    scrollAnchorRef.current?.scrollIntoView({
      behavior: "smooth",
      block: "end",
    });
  }, [messages, isResponding]);

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
      isResponding,
      recordId,
      replaceMessageContent,
    ],
  );

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
