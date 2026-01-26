// src/middleware.ts
import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

export function middleware(request: NextRequest) {
  const isAuthDisabled = process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";
  
  if (isAuthDisabled) {
    if (request.nextUrl.pathname.startsWith("/login")) {
      return NextResponse.redirect(new URL("/", request.url));
    }
    
    const orgSessionCookie = request.cookies.get("organizationSession");
    const hasOrgSession = orgSessionCookie?.value;
    
    if (request.nextUrl.pathname.startsWith("/select-org")) {
      if (hasOrgSession) {
        console.log("Org session exists, redirecting away from /select-org to home");
        return NextResponse.redirect(new URL("/", request.url));
      }
      return NextResponse.next();
    }
    
    if (!hasOrgSession) {
      console.log("No org session found, redirecting to /select-org");
      return NextResponse.redirect(new URL("/select-org", request.url));
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