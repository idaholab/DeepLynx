"use client";

import { Suspense, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useLanguage } from "@/app/contexts/Language";
import {
    getDeviceAuthorizationRequest,
    setDeviceAuthorizationDecision,
} from "@/app/lib/client_service/oauth_services.client";
import type { DeviceVerificationLookupResponseDto } from "@/app/lib/client_service/oauth_services.client";

type ApiError = {
    response?: {
        data?: string | {
            error?: string;
            error_description?: string;
            message?: string;
        };
    };
    message?: string;
};

function normalizeUserCode(value: string) {
    return value.trim().toUpperCase();
}

function getErrorMessage(error: unknown, fallbackMessage: string) {
    const apiError = error as ApiError;
    const data = apiError.response?.data;

    if (typeof data === "string") {
        return data;
    }

    return data?.error_description || data?.message || data?.error || apiError.message || fallbackMessage;
}

function formatDate(value: string) {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return date.toLocaleString();
}

function DeviceVerificationContent() {
    const { t } = useLanguage();
    const searchParams = useSearchParams();
    const codeFromUrl = searchParams.get("user_code") || "";
    const [userCode, setUserCode] = useState(codeFromUrl);
    const [deviceRequest, setDeviceRequest] = useState<DeviceVerificationLookupResponseDto | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const [decisionLoading, setDecisionLoading] = useState<"approve" | "deny" | null>(null);
    const normalizedUserCode = useMemo(() => normalizeUserCode(userCode), [userCode]);
    const canDecide = deviceRequest?.status === "pending";

    async function lookupRequest(code = normalizedUserCode) {
        if (!code) {
            setError(t.translations.OAUTH_DEVICE_ENTER_CODE);
            return;
        }

        setLoading(true);
        setError(null);

        try {
            const response = await getDeviceAuthorizationRequest(code);
            setDeviceRequest(response);
            setUserCode(response.user_code);
        } catch (lookupError) {
            setDeviceRequest(null);
            setError(getErrorMessage(lookupError, t.translations.OAUTH_DEVICE_UNABLE_TO_COMPLETE));
        } finally {
            setLoading(false);
        }
    }

    async function submitDecision(approve: boolean) {
        setDecisionLoading(approve ? "approve" : "deny");
        setError(null);

        try {
            const response = await setDeviceAuthorizationDecision(normalizedUserCode, approve);
            setDeviceRequest(response);
        } catch (decisionError) {
            setError(getErrorMessage(decisionError, t.translations.OAUTH_DEVICE_UNABLE_TO_COMPLETE));
        } finally {
            setDecisionLoading(null);
        }
    }

    useEffect(() => {
        const normalizedCodeFromUrl = normalizeUserCode(codeFromUrl);

        if (normalizedCodeFromUrl) {
            void lookupRequest(normalizedCodeFromUrl);
        }
    }, [codeFromUrl]);

    return (
        <main className="min-h-screen bg-gray-50 px-4 py-12 text-gray-900">
            <section className="mx-auto max-w-xl rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
                <div className="mb-6">
                    <p className="text-sm font-semibold uppercase text-gray-500">{t.translations.OAUTH_DEVICE_BRAND}</p>
                    <h1 className="mt-2 text-2xl font-semibold">{t.translations.OAUTH_DEVICE_TITLE}</h1>
                </div>

                <form
                    className="space-y-4"
                    onSubmit={(event) => {
                        event.preventDefault();
                        void lookupRequest();
                    }}
                >
                    <label className="block text-sm font-medium text-gray-700" htmlFor="user_code">
                        {t.translations.OAUTH_DEVICE_CODE}
                    </label>
                    <div className="flex flex-col gap-3 sm:flex-row">
                        <input
                            id="user_code"
                            name="user_code"
                            className="min-h-11 flex-1 rounded-md border border-gray-300 px-3 py-2 text-lg font-semibold uppercase outline-none focus:border-gray-700 focus:ring-2 focus:ring-gray-200"
                            value={userCode}
                            onChange={(event) => {
                                setUserCode(event.target.value.toUpperCase());
                                setDeviceRequest(null);
                                setError(null);
                            }}
                            autoComplete="off"
                        />
                        <button
                            type="submit"
                            className="min-h-11 rounded-md bg-gray-800 px-5 py-2 text-sm font-semibold text-white hover:bg-gray-700 disabled:cursor-not-allowed disabled:opacity-60"
                            disabled={loading}
                        >
                            {loading ? t.translations.OAUTH_DEVICE_CHECKING : t.translations.OAUTH_DEVICE_CONTINUE}
                        </button>
                    </div>
                </form>

                {error && (
                    <div className="mt-5 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                        {error}
                    </div>
                )}

                {deviceRequest && (
                    <div className="mt-6 space-y-5 border-t border-gray-200 pt-5">
                        <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2">
                            <div>
                                <dt className="font-medium text-gray-500">{t.translations.OAUTH_DEVICE_APPLICATION}</dt>
                                <dd className="mt-1 text-gray-900">{deviceRequest.application_name}</dd>
                            </div>
                            <div>
                                <dt className="font-medium text-gray-500">{t.translations.OAUTH_DEVICE_CLIENT_ID}</dt>
                                <dd className="mt-1 break-all text-gray-900">{deviceRequest.client_id}</dd>
                            </div>
                            <div>
                                <dt className="font-medium text-gray-500">{t.translations.OAUTH_DEVICE_STATUS}</dt>
                                <dd className="mt-1 capitalize text-gray-900">{deviceRequest.status}</dd>
                            </div>
                            <div>
                                <dt className="font-medium text-gray-500">{t.translations.OAUTH_DEVICE_EXPIRES}</dt>
                                <dd className="mt-1 text-gray-900">{formatDate(deviceRequest.expires_at)}</dd>
                            </div>
                            {deviceRequest.scope && (
                                <div className="sm:col-span-2">
                                    <dt className="font-medium text-gray-500">{t.translations.OAUTH_DEVICE_SCOPE}</dt>
                                    <dd className="mt-1 break-words text-gray-900">{deviceRequest.scope}</dd>
                                </div>
                            )}
                        </dl>

                        <div className="flex flex-col gap-3 sm:flex-row">
                            <button
                                type="button"
                                className="min-h-11 rounded-md bg-gray-800 px-5 py-2 text-sm font-semibold text-white hover:bg-gray-700 disabled:cursor-not-allowed disabled:opacity-60"
                                disabled={!canDecide || decisionLoading !== null}
                                onClick={() => void submitDecision(true)}
                            >
                                {decisionLoading === "approve" ? t.translations.OAUTH_DEVICE_APPROVING : t.translations.OAUTH_DEVICE_APPROVE}
                            </button>
                            <button
                                type="button"
                                className="min-h-11 rounded-md border border-gray-300 px-5 py-2 text-sm font-semibold text-gray-800 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-60"
                                disabled={!canDecide || decisionLoading !== null}
                                onClick={() => void submitDecision(false)}
                            >
                                {decisionLoading === "deny" ? t.translations.OAUTH_DEVICE_DENYING : t.translations.OAUTH_DEVICE_DENY}
                            </button>
                        </div>
                    </div>
                )}
            </section>
        </main>
    );
}

export default function DeviceVerificationPage() {
    return (
        <Suspense fallback={<main className="min-h-screen bg-gray-50 px-4 py-12 text-gray-900" />}>
            <DeviceVerificationContent />
        </Suspense>
    );
}