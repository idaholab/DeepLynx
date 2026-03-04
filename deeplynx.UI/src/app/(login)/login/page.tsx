// src/app/(login)/login/page.tsx

"use client";
import ArrowButton from "@/app/(home)/components/ArrowButton";
import { links, LinkT } from "@/app/(home)/links";
import { useLanguage } from "@/app/contexts/Language";
import "@/app/globals.css";
import Image from "next/image";
import Link from "next/link";
import { useState } from "react";

export default function Login() {
  const [isChecked, setChecked] = useState(true);
  const { t } = useLanguage();

  return (
    <div className="flex flex-col items-center justify-center login min-h-screen gap-4 sm:p-22 font-[family-name:var(--font-roboto-sans)] ">
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
            <div className="flex flex-col items-center"></div>

            <div className="flex flex-col items-center mt-2">
              <h2 className="text-sm text-center text-slate-800">
                {t.translations.WARNING}
              </h2>
              <h2 className="text-sm text-center text-slate-800">
                {t.translations.VULNERABILITY_DISCLOSURE}
              </h2>
            </div>
          </div>
        </div>
      </main>
      <Link className="text-white w-full max-w-lg px-4 sm:px-0" href="/login/signin">
        <button className="btn btn-outline w-full">
          {t.translations.SIGN_IN}
        </button>
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
  );
}
