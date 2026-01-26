// src/middleware.ts
import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { auth } from "../auth";

export async function middleware(request: NextRequest) {
  const isAuthDisabled = 
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";
  
  console.log(`[Middleware] Path: ${request.nextUrl.pathname}, Auth Disabled: ${isAuthDisabled}`);
  
  // ============================================================================
  // SECTION 1: Handle Auth Disabled Mode
  // ============================================================================
  if (isAuthDisabled) {
    if (request.nextUrl.pathname.startsWith("/login")) {
      console.log("[Middleware] Redirecting from /login to /");
      return NextResponse.redirect(new URL("/", request.url));
    }
    
    const orgSessionCookie = request.cookies.get("organizationSession");
    const hasOrgSession = !!orgSessionCookie?.value;
    
    console.log(`[Middleware] Has org session: ${hasOrgSession}`);
    
    if (request.nextUrl.pathname.startsWith("/select-org")) {
      console.log("[Middleware] On /select-org page");
      if (hasOrgSession) {
        console.log("[Middleware] Org session exists, redirecting away from /select-org to home");
        return NextResponse.redirect(new URL("/", request.url));
      }
      console.log("[Middleware] Allowing access to /select-org");
      return NextResponse.next();
    }
    
    if (!hasOrgSession) {
      console.log(`[Middleware] No org session found, redirecting ${request.nextUrl.pathname} to /select-org`);
      return NextResponse.redirect(new URL("/select-org", request.url));
    }
    
    console.log("[Middleware] Passing through (auth disabled)");
    return NextResponse.next();
  }
  
  // ============================================================================
  // SECTION 2: Handle Auth Enabled Mode (Check for Stale Sessions)
  // ============================================================================
  
  // Don't check auth on public routes
  const publicRoutes = [
    "/login",
    "/api/auth",
    "/_next",
    "/favicon.ico",
    "/assets"
  ];
  
  const isPublicRoute = publicRoutes.some(route => 
    request.nextUrl.pathname.startsWith(route)
  );
  
  if (isPublicRoute) {
    console.log("[Middleware] Public route, allowing through");
    return NextResponse.next();
  }
  
  let session;
  try {
    session = await auth();
    console.log(`[Middleware] Session check: ${session ? 'valid' : 'none'}`);
  } catch (error) {
    console.error("[Middleware] Session error (likely stale session):", error);
    
    const response = NextResponse.redirect(
      new URL("/login/signin?session_expired=true", request.url)
    );
    
    response.cookies.delete("next-auth.session-token");
    response.cookies.delete("__Secure-next-auth.session-token");
    response.cookies.delete("next-auth.csrf-token");
    response.cookies.delete("__Secure-next-auth.csrf-token");
    response.cookies.delete("next-auth.callback-url");
    response.cookies.delete("__Secure-next-auth.callback-url");
    
    response.cookies.delete("organizationSession");
    response.cookies.delete("projectSession");
    
    console.log("[Middleware] Cleared stale cookies, redirecting to login");
    return response;
  }
  
  if (!session) {
    console.log("[Middleware] No session, redirecting to login");
    return NextResponse.redirect(new URL("/login/signin", request.url));
  }
  
  if (session.error) {
    console.log(`[Middleware] Session has error: ${session.error}`);
    
    const response = NextResponse.redirect(
      new URL("/login/signin?session_expired=true", request.url)
    );
    
    response.cookies.delete("next-auth.session-token");
    response.cookies.delete("__Secure-next-auth.session-token");
    response.cookies.delete("organizationSession");
    response.cookies.delete("projectSession");
    
    return response;
  }
  
  console.log("[Middleware] Valid session, passing through");
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