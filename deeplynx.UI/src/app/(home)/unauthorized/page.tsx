import Link from "next/link";

export default function UnauthorizedPage() {
  return (
    <main className="flex min-h-[60vh] items-center justify-center px-6">
      <section className="max-w-md text-center">
        <h1 className="text-2xl font-semibold">Unauthorized</h1>
        <p className="mt-3 text-base-content/70">
          You do not have access to this page.
        </p>
        <Link href="/" className="btn btn-primary mt-6">
          Return home
        </Link>
      </section>
    </main>
  );
}
