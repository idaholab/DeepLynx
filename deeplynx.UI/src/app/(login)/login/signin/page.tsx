// src/app/(login)/login/signin/page.tsx

"use client";
import ArrowButton from "@/app/(home)/components/ArrowButton";
import { links, LinkT } from "@/app/(home)/links";
import { useLanguage } from "@/app/contexts/Language";
import "@/app/globals.css";
import Image from "next/image";
import Link from "next/link";
import { useState, useEffect, Suspense } from "react";
import { signIn } from "next-auth/react";
import { useRouter, useSearchParams } from "next/navigation";
import { useSafeSession } from "@/app/hooks/useSafeSession";
import TopBanner from "@/app/(home)/components/VulnerabilityBanner";

function SigninContent() {
  const [isChecked, setChecked] = useState(true);
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [hasAcknowledged, setHasAcknowledged] = useState(false);
  const [showModal, setShowModal] = useState(true);
  const { data: session, status } = useSafeSession();
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnUrl = searchParams.get("returnUrl");
  const { t } = useLanguage();

  // Check if auth is disabled
  const isAuthDisabled =
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

  useEffect(() => {
    // If auth is disabled, redirect immediately to home
    if (isAuthDisabled) {
      router.push("/");
      return;
    }

    // If user is already authenticated and there's a returnUrl, redirect to it
    if (status === "authenticated" && returnUrl) {
      console.log(`User authenticated, redirecting to: ${returnUrl}`);
      router.push(returnUrl);
    } else if (status === "authenticated") {
      // If authenticated but no returnUrl, go home
      router.push("/");
    }
  }, [status, router, isAuthDisabled, returnUrl]);

  // If auth is disabled, show loading while redirecting
  if (isAuthDisabled) {
    return (
      <div className="flex flex-col items-center justify-center login min-h-screen gap-4 sm:p-22 font-[family-name:var(--font-roboto-sans)]">
        <div className="flex flex-col items-center sm:items-start mb-0">
          <Image
            src="/assets/nexusWhite.png"
            alt="DeepLynx logo"
            width={265.8}
            height={113.9}
            priority
          />
        </div>
        <div className="text-center text-white">
          <div className="loading loading-spinner loading-lg"></div>
          <p className="mt-4">Redirecting...</p>
        </div>
      </div>
    );
  }

  // Show loading while checking authentication status
  if (status === "loading") {
    return (
      <div className="flex flex-col items-center justify-center login min-h-screen gap-4 sm:p-22 font-[family-name:var(--font-roboto-sans)]">
        <div className="flex flex-col items-center sm:items-start mb-0">
          <Image
            src="/assets/nexusWhite.png"
            alt="DeepLynx logo"
            width={265.8}
            height={113.9}
            priority
          />
        </div>
        <div className="text-center text-white">
          <div className="loading loading-spinner loading-lg"></div>
          <p className="mt-4">Loading...</p>
        </div>
      </div>
    );
  }

  // If authenticated, show loading while redirecting
  if (status === "authenticated") {
    return (
      <div className="flex flex-col items-center justify-center login min-h-screen gap-4 sm:p-22 font-[family-name:var(--font-roboto-sans)]">
        <div className="flex flex-col items-center sm:items-start mb-0">
          <Image
            src="/assets/nexusWhite.png"
            alt="DeepLynx logo"
            width={265.8}
            height={113.9}
            priority
          />
        </div>
        <div className="text-center text-white">
          <div className="loading loading-spinner loading-lg"></div>
          <p className="mt-4">Redirecting...</p>
        </div>
      </div>
    );
  }

  const handleOktaSignIn = async () => {
    if (!hasAcknowledged) {
      return;
    }

    setIsSigningIn(true);

    // Construct the callback URL to include the returnUrl
    const callbackUrl = returnUrl || "/";

    console.log(`Signing in with Okta, will redirect to: ${callbackUrl}`);

    await signIn("okta", {
      callbackUrl: callbackUrl,
      redirect: true,
    });
  };

  const handleAcknowledge = () => {
    setHasAcknowledged(true);
    setShowModal(false);
  };

  return (
    <div className="flex flex-col min-h-screen">
      {/* Top Banner */}
      <TopBanner />

      <div className="flex flex-col items-center justify-center login min-h-screen gap-4 sm:p-22 font-[family-name:var(--font-roboto-sans)] pt-10">
        {/* Acknowledgment Modal */}
        {showModal && (
          <div className="fixed inset-0 z-50 flex items-center justify-center login">
            <div className="bg-white rounded-2xl shadow-2xl max-w-2xl w-full mx-4 p-6">
              <h2 className="text-2xl font-bold text-gray-800 mb-4">
                System Use Notification
              </h2>
              <div className="text-gray-700 mb-6 space-y-3 text-sm max-h-96 overflow-y-auto">
                <p>
                  This is a DOE computer system. DOE computer systems are provided for the processing of official U.S. Government information only.
                </p>
                <p>
                  All data contained within DOE computer systems is owned by DOE and may be audited, intercepted, recorded, read, copied, or captured in any manner and disclosed in any manner by authorized personnel.
                </p>
                <p>
                  THERE IS NO RIGHT OF PRIVACY IN THIS SYSTEM. System personnel may disclose any potential evidence of crime found on DOE computer systems to appropriate authorities.
                </p>
                <p>
                  USE OF THIS SYSTEM BY ANY USER, AUTHORIZED OR UNAUTHORIZED, CONSTITUTES CONSENT TO THIS AUDITING, INTERCEPTION, RECORDING, READING, COPYING, CAPTURING, and DISCLOSURE OF COMPUTER ACTIVITY.
                </p>
                <p className="font-bold text-red-600 text-center text-base mt-4">
                  **WARNING**WARNING**WARNING**WARNING**WARNING**
                </p>
              </div>
              <button
                onClick={handleAcknowledge}
                className="w-full py-4 text-sm text-center text-gray-50 bg-gray-700 border-2 border-black rounded-xl hover:bg-gray-600 transition-colors"
              >
                I Acknowledge
              </button>
            </div>
          </div>
        )}

        <div className="flex flex-col items-center sm:items-start mb-0">
          <Image
            src="/assets/nexusWhite.png"
            alt="DeepLynx logo"
            width={265.8}
            height={113.9}
            priority
          />
        </div>
        <main className="flex flex-col items-center w-full max-w-lg mt-0 mb-2">
          <div className="w-full p-2 bg-white border-2 border-solid rounded-3xl">
            <div className="fieldset m-5">
              <div className="flex flex-col items-center">
                <button
                  onClick={handleOktaSignIn}
                  disabled={isSigningIn || !hasAcknowledged}
                  className="w-70 py-4 mx-5 text-sm text-center text-gray-50 bg-gray-700 border-2 border-black rounded-xl hover:bg-gray-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                >
                  {isSigningIn && (
                    <span className="loading loading-spinner loading-sm"></span>
                  )}
                  {t.translations.PIV_CAC_CARD_SIGN_IN}
                </button>
                {!hasAcknowledged && (
                  <p className="mt-2 text-sm text-gray-600">
                    Please acknowledge the notice to continue
                  </p>
                )}
              </div>
            </div>
          </div>
        </main>
        <Link
          className="text-white hover:bg-[#383838] dark:hover:bg-[#ccc]"
          href="/"
        >
          {" "}
          <u>{t.translations.TROUBLE_LOGGING_IN}</u>
        </Link>
        <footer className="flex flex-wrap items-center justify-center gap-8 mt-16 mb-8">
          {/* {links
          .filter(
            (link: LinkT) =>
              link.text.toLowerCase().includes("about") ||
              link.text.toLowerCase().includes("contact")
          )
          .map((link: LinkT, i: number) => (
            <ArrowButton key={i} text={link.text} href={link.href} />
          ))} */}
        </footer>
      </div>
    </div>
  );
}

export default function Signin() {
  return (
    <Suspense
      fallback={
        <div className="flex flex-col items-center justify-center login min-h-screen gap-4 sm:p-22 font-[family-name:var(--font-roboto-sans)]">
          <div className="flex flex-col items-center sm:items-start mb-0">
            <Image
              src="/assets/nexusWhite.png"
              alt="DeepLynx logo"
              width={265.8}
              height={113.9}
              priority
            />
          </div>
          <div className="text-center text-white">
            <div className="loading loading-spinner loading-lg"></div>
            <p className="mt-4">Loading...</p>
          </div>
        </div>
      }
    >
      <SigninContent />
    </Suspense>
  );
}