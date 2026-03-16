"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import { streamInsightQuery } from "@/app/lib/client_service/insight_services.client";
import { useLanguage } from "@/app/contexts/Language";
import { PaperAirplaneIcon } from "@heroicons/react/24/solid";

type InsightRole = "assistant" | "user";

type InsightMessage = {
  id: number;
  role: InsightRole;
  content: string;
  timestamp: string;
};

interface ProjectInsightChatProps {
  projectName: string;
  scopedRecordIds: number[];
}

function getCurrentTimestamp(): string {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date());
}

function withTokens(
  template: string,
  values: Record<string, string | number>,
): string {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template,
  );
}

export default function ProjectInsightChat({
  projectName,
  scopedRecordIds,
}: ProjectInsightChatProps) {
  const { t } = useLanguage();
  const scopeCount = scopedRecordIds.length;
  const messageIdRef = useRef(1);
  const scrollAnchorRef = useRef<HTMLDivElement>(null);
  const promptInputRef = useRef<HTMLTextAreaElement>(null);
  const [draft, setDraft] = useState("");
  const [isResponding, setIsResponding] = useState(false);
  const [messages, setMessages] = useState<InsightMessage[]>([]);

  const introMessage = useMemo(
    () =>
      withTokens(
        scopeCount > 0
          ? t.translations.PROJECT_INSIGHT_CHAT_INTRO_READY
          : t.translations.PROJECT_INSIGHT_CHAT_INTRO_EMPTY,
        {
          projectName,
          recordCount: scopeCount,
        },
      ),
    [projectName, scopeCount, t.translations],
  );

  useEffect(() => {
    messageIdRef.current = 1;
    setDraft("");
    setIsResponding(false);
    setMessages([
      {
        id: messageIdRef.current++,
        role: "assistant",
        content: introMessage,
        timestamp: getCurrentTimestamp(),
      },
    ]);
  }, [introMessage]);

  useEffect(() => {
    scrollAnchorRef.current?.scrollIntoView({
      behavior: "smooth",
      block: "end",
    });
  }, [messages, isResponding]);

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

  async function handleSend(input: string) {
    const prompt = input.trim();
    if (!prompt || isResponding) return;

    if (scopeCount === 0) {
      setMessages((prev) => [
        ...prev,
        createMessage("assistant", t.translations.PROJECT_INSIGHT_SCOPE_EMPTY),
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
          fileIds: scopedRecordIds,
        },
        (chunk) => appendMessageChunk(assistantMessage.id, chunk),
      );

      if (!responseText.trim()) {
        replaceMessageContent(
          assistantMessage.id,
          t.translations.INSIGHT_EMPTY_RESPONSE,
        );
      }
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
      promptInputRef.current?.focus();
    }
  }

  return (
    <section className="card border border-base-300/60 bg-base-100 shadow-lg h-full min-h-0">
      <div className="card-body flex h-full min-h-0 flex-col gap-4 p-5 lg:p-6">
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div className="flex flex-wrap items-end gap-2">
            <span className="text-xs text-base-content/60">
              {t.translations.INSIGHT_CONVERSATION_NOT_SAVED}
            </span>
          </div>
          <span className="badge badge-outline badge-secondary">
            {withTokens(t.translations.PROJECT_INSIGHT_SCOPE_COUNT, {
              count: scopeCount,
            })}
          </span>
        </div>

        <div className="flex-1 min-h-0 rounded-box border border-base-300 bg-base-200/70">
          <div className="h-full overflow-y-auto px-4 py-4">
            <div className="space-y-4">
              {messages.map((message) => (
                <div
                  key={message.id}
                  className={`chat ${message.role === "user" ? "chat-end" : "chat-start"}`}
                >
                  <div className="chat-header mb-1 text-xs text-base-content/60">
                    {message.role === "user"
                      ? t.translations.INSIGHT_YOU
                      : t.translations.INSIGHT}
                    <time className="ml-2">{message.timestamp}</time>
                  </div>
                  <div
                    className={`chat-bubble whitespace-pre-wrap ${
                      message.role === "user"
                        ? "chat-bubble-primary"
                        : "border border-base-300 bg-base-100 text-base-content"
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
        </div>

        <form
          className="shrink-0 flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            const prompt = draft;
            setDraft("");
            void handleSend(prompt);
          }}
        >
          <textarea
            ref={promptInputRef}
            className="textarea textarea-bordered min-h-24 max-h-40 w-full resize-none"
            placeholder={t.translations.PROJECT_INSIGHT_CHAT_PLACEHOLDER}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                const prompt = draft;
                setDraft("");
                void handleSend(prompt);
              }
            }}
            disabled={isResponding}
          />

          <div className="flex items-center justify-between gap-3">
            <p className="text-xs text-base-content/60">
              {scopeCount > 0
                ? withTokens(t.translations.PROJECT_INSIGHT_SCOPE_COUNT, {
                    count: scopeCount,
                  })
                : t.translations.PROJECT_INSIGHT_SCOPE_EMPTY}
            </p>
            <button
              type="submit"
              className="btn btn-primary gap-2 self-end"
              disabled={!draft.trim() || isResponding}
              aria-label={t.translations.INSIGHT_SEND_PROMPT_ARIA}
            >
              <PaperAirplaneIcon className="size-4" />
              {t.translations.INSIGHT_SEND}
            </button>
          </div>
        </form>
      </div>
    </section>
  );
}
