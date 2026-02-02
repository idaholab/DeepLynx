// src/middleware.ts
import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { auth } from "../auth";
import { auth } from "../auth";

export const runtime = "nodejs";

export async function middleware(request: NextRequest) {
  const isAuthDisabled = process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";
  const pathname = request.nextUrl.pathname;
  
  // ============================================================================
  // SECTION 1: Define Public Routes (no auth needed)
  // ============================================================================
  const publicRoutes = [
    "/login",
    "/api/auth",
    "/_next",
    "/favicon.ico",
    "/assets",
    "/images"
  ];
  
  const isPublicRoute = publicRoutes.some(route => pathname.startsWith(route));
  
  // ============================================================================
  // SECTION 2: Handle Auth DISABLED Mode
  // ============================================================================
  if (isAuthDisabled) {
    // If auth is disabled and user is trying to access login pages, redirect to home
    if (pathname.startsWith("/login")) {
      return NextResponse.redirect(new URL("/", request.url));
    }
    
    // Redirect org selection page if auth is disabled
    if (pathname.startsWith("/select-org")) {
      return NextResponse.redirect(new URL("/", request.url));
    }
    
    return NextResponse.next();
  }
  
  // ============================================================================
  // SECTION 3: Handle Auth ENABLED Mode
  // ============================================================================
  
  // Allow public routes without authentication
  if (isPublicRoute) {
    return NextResponse.next();
  }
  
  // STEP 1: Check Authentication FIRST
  let session;
  try {
    session = await auth();
  } catch (error) {
    console.error("Auth error in middleware:", error);
    
    const response = NextResponse.redirect(
      new URL("/login/signin?session_expired=true", request.url)
    );
    
    const cookiesToClear = [
      "next-auth.session-token",
      "__Secure-next-auth.session-token",
      "next-auth.csrf-token",
      "__Secure-next-auth.csrf-token",
      "next-auth.callback-url",
      "__Secure-next-auth.callback-url",
      "organizationSession",
      "projectSession"
    ];
    
    cookiesToClear.forEach(cookieName => {
      response.cookies.delete(cookieName);
    });
    
    return response;
  }
  
  if (!session) {
    return NextResponse.redirect(new URL("/login/signin", request.url));
  }
  
  if (session.error) {
    const response = NextResponse.redirect(
      new URL("/login/signin?session_expired=true", request.url)
    );
    
    response.cookies.delete("next-auth.session-token");
    response.cookies.delete("__Secure-next-auth.session-token");
    response.cookies.delete("organizationSession");
    response.cookies.delete("projectSession");
    
    return response;
  }
  
  // STEP 2: User is authenticated, NOW check org selection
  const orgSessionCookie = request.cookies.get("organizationSession");
  const hasOrgSession = !!orgSessionCookie?.value;
  
  if (pathname.startsWith("/select-org")) {
    if (hasOrgSession) {
      return NextResponse.redirect(new URL("/", request.url));
    }
    return NextResponse.next();
  }
  
  if (!hasOrgSession) {
    return NextResponse.redirect(new URL("/select-org", request.url));
    return NextResponse.next();
  }
  
  if (!hasOrgSession) {
    return NextResponse.redirect(new URL("/select-org", request.url));
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
    "/((?!api|_next/static|_next/image|favicon.ico|assets|images).*)",
  ],
};