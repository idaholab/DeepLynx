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

    await signIn("okta", {
      callbackUrl: callbackUrl,
      redirect: true,
    });
  };

  const handleAcknowledge = () => {
    setHasAcknowledged(true);
  };

  return (
    <div className="flex flex-col min-h-screen">
      {/* Top Banner */}
      <TopBanner />

      <div className="flex flex-col items-center justify-center login min-h-screen gap-4 sm:p-22 font-[family-name:var(--font-roboto-sans)] pt-10">
        <Image
          src="/assets/nexusWhite.png"
          alt="DeepLynx logo"
          width={265.8}
          height={113.9}
          priority
        />
        <main className="flex flex-col items-center w-full max-w-lg">
          <div className="w-full bg-white rounded-2xl shadow-2xl overflow-hidden">
            {/* Swappable body */}
            <div className="relative overflow-hidden">
              {/* Acknowledgement panel */}
              <div
                className="p-6 transition-all duration-500"
                style={{
                  opacity: hasAcknowledged ? 0 : 1,
                  transform: hasAcknowledged ? "translateX(-100%)" : "translateX(0)",
                  position: hasAcknowledged ? "absolute" : "relative",
                  width: "100%",
                }}
              >
                <h2 className="text-2xl font-bold text-gray-800 mb-4 text-center">
                  System Use Notification
                </h2>
                <div className="text-gray-700 mb-6 space-y-3 text-sm">
                  <p>
                    This is a DOE computer system. DOE computer systems are
                    provided for the processing of official U.S. Government
                    information only.
                  </p>
                  <p>
                    All data contained within DOE computer systems is owned by DOE
                    and may be audited, intercepted, recorded, read, copied, or
                    captured in any manner and disclosed in any manner by
                    authorized personnel.
                  </p>
                  <p>
                    THERE IS NO RIGHT OF PRIVACY IN THIS SYSTEM. System personnel
                    may disclose any potential evidence of crime found on DOE
                    computer systems to appropriate authorities.
                  </p>
                  <p>
                    USE OF THIS SYSTEM BY ANY USER, AUTHORIZED OR UNAUTHORIZED,
                    CONSTITUTES CONSENT TO THIS AUDITING, INTERCEPTION, RECORDING,
                    READING, COPYING, CAPTURING, and DISCLOSURE OF COMPUTER
                    ACTIVITY.
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

              {/* Sign-in panel */}
              <div
                className="p-6 transition-all duration-500"
                style={{
                  opacity: hasAcknowledged ? 1 : 0,
                  transform: hasAcknowledged ? "translateX(0)" : "translateX(100%)",
                  position: hasAcknowledged ? "relative" : "absolute",
                  width: "100%",
                }}
              >
                <div className="flex flex-col items-center gap-4">
                  <button
                    onClick={handleOktaSignIn}
                    disabled={isSigningIn}
                    className="w-full py-4 text-sm text-center text-gray-50 bg-gray-700 border-2 border-black rounded-xl hover:bg-gray-600 transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2"
                  >
                    {isSigningIn && (
                      <span className="loading loading-spinner loading-sm"></span>
                    )}
                    {t.translations.SIGN_IN}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </main>

        <Link
          className="text-white hover:bg-[#383838] dark:hover:bg-[#ccc]"
          href="https://inl.gov/privacy-and-accessibility/"
          target="_blank"
          rel="noopener noreferrer"
        >
          <u>{t.translations.PRIVACY}</u>
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
