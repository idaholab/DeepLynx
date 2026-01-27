// src/middleware.ts
import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

export function middleware(request: NextRequest) {
  const isAuthDisabled = process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";
  
  if (isAuthDisabled) {
    // If auth is disabled and user is trying to access login pages, redirect to home
    if (request.nextUrl.pathname.startsWith("/login")) {
      return NextResponse.redirect(new URL("/", request.url));
    }
    
    // Optional: Also redirect org selection page if auth is disabled
    // (since dev user likely has a default org)
    if (request.nextUrl.pathname.startsWith("/select-org")) {
      return NextResponse.redirect(new URL("/", request.url));
    }
  }
  
  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths except for the ones starting with:
     * - api (API routes)
     * - _next/static (static files)
     * - _next/image (image optimization files)
     * - favicon.ico (favicon file)
     * - assets (your static assets)
     */
    "/((?!api|_next/static|_next/image|favicon.ico|assets).*)",
  ],
};