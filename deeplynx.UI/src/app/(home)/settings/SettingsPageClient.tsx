// src/app/(home)/settings/SettingsPageClient.tsx
"use client";

import { useLanguage } from "@/app/contexts/Language";
import {
  createApiKey,
  deleteApiKey,
  getAllKeysByUser,
} from "@/app/lib/client_service/settings_services.client";
import { PlusIcon, TrashIcon } from "@heroicons/react/24/outline";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import toast from "react-hot-toast";
import AvatarCell from "../components/Avatar";
import UserSettingsSkeleton from "../components/skeletons/usersettingsskeleton";
import ThemeToggle from "../components/ThemeToggle";
import ToastInfoModal from "../components/ToastInfoModal";

const SettingsPageClient = () => {
  const { lang, setLang, t } = useLanguage();
  const { data: session } = useSession();
  const [userKeys, setUserKeys] = useState<string[]>([]);
  const name = session?.user?.name ?? "";
  const email = session?.user?.email ?? "";
  const image = session?.user?.image ?? undefined;
  const [creating, setCreating] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();

  const apiKeyGen = async () => {
    try {
      setCreating(true);
      const res = await createApiKey();
      const keyStr = "Key: " + res.apiKey;
      const secretStr = "Secret: " + res.apiSecret;
      setUserKeys((prev) => [res.apiKey, ...prev]);
      toast.success(
        (toastInstance) => (
          <ToastInfoModal
            title={t.translations.API_KEYPAIR_CREATED_SUCCESS}
            toastId={toastInstance.id}
            infoDisplay={[keyStr, secretStr]}
          />
        ),
        {
          duration: Infinity,
          style: {
            maxWidth: "none",
          },
        }
      );
    } catch (error) {
      console.error("Error creating keypair:", error);
      toast.error(t.translations.API_KEYPAIR_CREATION_FAILED);
    } finally {
      setCreating(false);
    }
  };

  const apiKeyDelete = (key: string) => async (e: React.MouseEvent) => {
    try {
      setDeleting(true);
      const res = await deleteApiKey(key);
      setUserKeys((prev) => prev.filter((x) => x !== key));
      toast.success(t.translations.API_KEYPAIR_DELETED_SUCCESS);
    } catch (error) {
      console.error("Error deleteing keypair:", error);
      toast.error(t.translations.API_KEYPAIR_DELETION_FAILED);
    } finally {
      setDeleting(false);
    }
  };

  useEffect(() => {
    (async () => {
      try {
        const keys = await getAllKeysByUser();
        setUserKeys(keys);
        setIsLoading(false);
      } catch (error) {
        console.error(error);
      }
    })();
  }, []);

  return (
    <div className="min-h-screen bg-gradient-to-br from-base-100 to-base-200 p-6 lg:p-12">
      <div className="max-w-5xl mx-auto">
        {/* User Header Section */}
        <div className="card bg-base-100 shadow-xl mb-8">
          <div className="card-body">
            <div className="flex items-center gap-4">
              <div className="avatar">
                <div className="w-20 rounded-full ring ring-primary ring-offset-base-100 ring-offset-2">
                  <AvatarCell image={image} name={name} size={20} />
                </div>
              </div>
              <div>
                <h1 className="text-3xl font-bold text-base-content">
                  {session?.user?.name}
                </h1>
                <p className="text-base-content/60 text-sm mt-1">{email}</p>
              </div>
            </div>
          </div>
        </div>

        {/* Main Content Grid */}
        <div className="grid gap-6">
          {/* User Settings Section */}
          <div className="card bg-base-100 shadow-xl">
            <div className="card-body">
              <h2 className="card-title text-xl mb-4">
                {t.translations.USER_SETTINGS ?? "User Settings"}
              </h2>

              <div className="space-y-4">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between py-3 border-b border-base-300">
                  <span className="font-semibold text-base-content/80 text-sm uppercase tracking-wider">
                    {t.translations.NAME ?? "Name"}
                  </span>
                  <span className="text-base-content font-medium">{name}</span>
                </div>

                <div className="flex flex-col sm:flex-row sm:items-center justify-between py-3 border-b border-base-300">
                  <span className="font-semibold text-base-content/80 text-sm uppercase tracking-wider">
                    {t.translations.EMAIL ?? "Email"}
                  </span>
                  <span className="text-base-content font-medium">{email ?? "—"}</span>
                </div>
              </div>
            </div>
          </div>

          {/* Preferences Section */}
          <div className="card bg-base-100 shadow-xl">
            <div className="card-body">
              <h2 className="card-title text-xl mb-4">
                {t.translations.PREFERENCES ?? "Preferences"}
              </h2>

              <div className="space-y-4">
                <div className="flex items-center justify-between py-3 border-b border-base-300">
                  <div>
                    <span className="font-semibold text-base-content block">
                      {t.translations.DARK_MODE ?? "Dark Mode"}
                    </span>
                    <span className="text-xs text-base-content/60">
                      {t.translations.DARK_MODE_DESCRIPTION}
                    </span>
                  </div>
                  <ThemeToggle />
                </div>

                <div className="flex items-center justify-between py-3">
                  <div>
                    <span className="font-semibold text-base-content block">
                      {t.translations.LANGUAGE ?? "Language"}
                    </span>
                    <span className="text-xs text-base-content/60">
                      {t.translations.LANGUAGE_DESCRIPTION}
                    </span>
                  </div>
                  <select
                    className="select select-bordered select-primary select-sm w-32"
                    value={lang}
                    onChange={(e) => setLang(e.target.value as "en" | "es")}
                  >
                    <option value="en">English</option>
                    <option value="es">Español</option>
                  </select>
                </div>
              </div>
            </div>
          </div>

          {/* API Keypairs Section */}
          <div className="card bg-base-100 shadow-xl">
            <div className="card-body">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h2 className="card-title text-xl">
                    {t.translations.API_KEYPAIRS ?? "API Keypairs"}
                  </h2>
                  <p className="text-sm text-base-content/60 mt-1">
                    {t.translations.API_KEYS_DESCRIPTION}
                  </p>
                </div>
                <button
                  type="button"
                  className="btn btn-primary gap-2"
                  onClick={apiKeyGen}
                  disabled={creating}
                >
                  <PlusIcon className="size-5" />
                  <span>{creating ? "Generating..." : t.translations.GENERATE_NEW}</span>
                </button>
              </div>

              {isLoading ? (
                <UserSettingsSkeleton />
              ) : (
                <div className="mt-4">
                  {userKeys.length > 0 ? (
                    <div className="space-y-3">
                      {userKeys.map((k, i) => (
                        <div
                          key={k}
                          className="flex items-center gap-3 p-4 bg-base-200 rounded-lg hover:bg-base-300 transition-colors"
                        >
                          <div className="badge badge-neutral badge-sm">{i + 1}</div>
                          <code className="flex-1 text-sm font-mono truncate">
                            {k}
                          </code>
                          <button
                            className="btn btn-ghost btn-sm btn-square text-error hover:bg-error/10"
                            type="button"
                            onClick={apiKeyDelete(k)}
                            disabled={deleting}
                            aria-label="Delete API key"
                          >
                            <TrashIcon className="size-5" />
                          </button>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="text-center py-12">
                      <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-base-200 mb-4">
                        <PlusIcon className="size-8 text-base-content/40" />
                      </div>
                      <p className="text-base-content/60">
                        {t.translations.NO_API_KEYS_CONFIGURED}
                      </p>
                      <p className="text-sm text-base-content/40 mt-1">
                        {t.translations.GENERATE_FIRST_KEYPAIR}
                      </p>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default SettingsPageClient;