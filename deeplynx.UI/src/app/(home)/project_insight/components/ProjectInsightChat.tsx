"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";
import InsightModelSettingsModal from "@/app/(home)/components/insight/InsightModelSettingsModal";
import {
  buildInsightModelBadges,
  formatInsightTimestamp,
} from "@/app/(home)/components/insight/insightChat.utils";
import type { InsightModelSelection } from "@/app/(home)/components/insight/useInsightModelSelection";
import { streamInsightQuery } from "@/app/lib/client_service/insight_services.client";
import { useLanguage } from "@/app/contexts/Language";
import { Cog6ToothIcon } from "@heroicons/react/24/outline";
import { PaperAirplaneIcon } from "@heroicons/react/24/solid";

type InsightRole = "assistant" | "user";

type InsightMessage = {
  id: number;
  role: InsightRole;
  content: string;
  timestamp: string;
};

interface ProjectInsightChatProps {
  organizationId?: number;
  projectId?: number;
  projectName: string;
  scopedRecordIds: number[];
  selectedInsightModels: InsightModelSelection;
  onSelectedInsightModelsChange: (
    nextSelection: InsightModelSelection,
  ) => void;
  isChatUnavailable?: boolean;
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
  organizationId,
  projectId,
  projectName,
  scopedRecordIds,
  selectedInsightModels,
  onSelectedInsightModelsChange,
  isChatUnavailable = false,
}: ProjectInsightChatProps) {
  const { t } = useLanguage();
  const scopeCount = scopedRecordIds.length;
  const messageIdRef = useRef(1);
  const scrollAnchorRef = useRef<HTMLDivElement>(null);
  const promptInputRef = useRef<HTMLTextAreaElement>(null);
  const [draft, setDraft] = useState("");
  const [isResponding, setIsResponding] = useState(false);
  const [isSettingsModalOpen, setIsSettingsModalOpen] = useState(false);
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
  const selectedModelBadges = buildInsightModelBadges(
    selectedInsightModels,
    t.translations.INSIGHT_NEXUS_MODEL,
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
        timestamp: formatInsightTimestamp(),
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
      timestamp: formatInsightTimestamp(),
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
    if (!organizationId || !projectId) {
      setMessages((prev) => [
        ...prev,
        createMessage("assistant", t.translations.INSIGHT_UNKNOWN_ERROR),
      ]);
      return;
    }

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
      const assistantResponseText = await streamInsightQuery(
        {
          organizationId,
          projectId,
          question: prompt,
          fileIds: scopedRecordIds,
          languageModelConfigId:
            selectedInsightModels.queryModelConfigId ?? undefined,
          embeddingModelConfigId:
            selectedInsightModels.embeddingModelConfigId ?? undefined,
        },
        (responseChunk) =>
          appendMessageChunk(assistantMessage.id, responseChunk),
      );

      if (!assistantResponseText.trim()) {
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
          <div className="flex flex-col gap-2">
            <span className="text-xs text-base-content/60">
              {t.translations.INSIGHT_CONVERSATION_NOT_SAVED}
            </span>
            {selectedModelBadges.length > 0 ? (
              <div className="flex flex-wrap gap-2">
                {selectedModelBadges.map((badgeLabel) => (
                  <span
                    key={badgeLabel}
                    className="badge badge-outline badge-sm text-base-content/80"
                  >
                    {badgeLabel}
                  </span>
                ))}
              </div>
            ) : null}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <span className="badge badge-outline badge-secondary">
              {withTokens(t.translations.PROJECT_INSIGHT_SCOPE_COUNT, {
                count: scopeCount,
              })}
            </span>
            <button
              type="button"
              className="btn btn-ghost btn-sm btn-circle"
              onClick={() => setIsSettingsModalOpen(true)}
              title={t.translations.INSIGHT_MODEL_SETTINGS}
            >
              <Cog6ToothIcon className="size-5" />
            </button>
          </div>
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
            disabled={isResponding || isChatUnavailable}
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
              disabled={!draft.trim() || isResponding || isChatUnavailable}
              aria-label={t.translations.INSIGHT_SEND_PROMPT_ARIA}
            >
              <PaperAirplaneIcon className="size-4" />
              {t.translations.INSIGHT_SEND}
            </button>
          </div>
        </form>
      </div>
      <InsightModelSettingsModal
        isOpen={isSettingsModalOpen}
        organizationId={organizationId}
        projectId={projectId}
        selectedInsightModels={selectedInsightModels}
        onClose={() => setIsSettingsModalOpen(false)}
        onSaveSelection={onSelectedInsightModelsChange}
      />
    </section>
  );
}
