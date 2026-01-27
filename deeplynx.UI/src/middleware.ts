// src/middleware.ts
import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { auth } from "../auth";

// ✅ Helper function for structured logging
function log(
  level: "INFO" | "WARN" | "ERROR",
  message: string,
  data?: unknown,
) {
  const timestamp = new Date().toISOString();
  const logData = data ? ` | Data: ${JSON.stringify(data)}` : "";
  console.log(`[${timestamp}] [MIDDLEWARE] [${level}] ${message}${logData}`);
}

export async function middleware(request: NextRequest) {
  const isAuthDisabled = 
    process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";
  
  const pathname = request.nextUrl.pathname;
  
  log('INFO', `Request started`, { 
    path: pathname, 
    authDisabled: isAuthDisabled,
    hasOrgCookie: !!request.cookies.get("organizationSession"),
    hasAuthCookie: !!request.cookies.get("next-auth.session-token") || !!request.cookies.get("__Secure-next-auth.session-token")
  });
  
  // ============================================================================
  // SECTION 1: Handle Auth Disabled Mode
  // ============================================================================
  if (isAuthDisabled) {
    log('INFO', 'Auth is DISABLED - checking org session');
    
    if (pathname.startsWith("/login")) {
      log('INFO', 'Redirecting from /login to home (auth disabled)');
      return NextResponse.redirect(new URL("/", request.url));
    }
    
    const orgSessionCookie = request.cookies.get("organizationSession");
    const hasOrgSession = !!orgSessionCookie?.value;
    
    if (pathname.startsWith("/select-org")) {
      log('INFO', 'User on /select-org page', { hasOrgSession });
      if (hasOrgSession) {
        log('INFO', 'Org already selected, redirecting to home');
        return NextResponse.redirect(new URL("/", request.url));
      }
      log('INFO', 'Allowing access to /select-org (no org selected yet)');
      return NextResponse.next();
    }
    
    if (!hasOrgSession) {
      log('WARN', 'No org session found, redirecting to /select-org', { from: pathname });
      return NextResponse.redirect(new URL("/select-org", request.url));
    }
    
    log('INFO', 'Auth disabled, org session exists, allowing through');
    return NextResponse.next();
  }
  
  // ============================================================================
  // SECTION 2: Handle Auth Enabled Mode (Check for Stale Sessions)
  // ============================================================================
  
  log('INFO', 'Auth is ENABLED - checking session validity');
  
  // Don't check auth on public routes
  const publicRoutes = [
    "/login",
    "/api/auth",
    "/_next",
    "/favicon.ico",
    "/assets"
  ];
  
  const isPublicRoute = publicRoutes.some(route => pathname.startsWith(route));
  
  if (isPublicRoute) {
    log('INFO', 'Public route, allowing through', { path: pathname });
    return NextResponse.next();
  }
  
  // ✅ TRY to get the session - this is where stale sessions will fail
  let session;
  try {
    session = await auth();
    log('INFO', `Session check complete`, { 
      hasSession: !!session,
      hasError: session?.error || false
    });
  } catch (error) {
    // ✅ CATCH: Session decode/verification failed (stale session after deployment!)
    log('ERROR', 'Session verification failed - likely stale session', { 
      error: error instanceof Error ? error.message : String(error),
      path: pathname
    });
    
    // Clear all auth-related cookies to prevent loops
    const response = NextResponse.redirect(
      new URL("/login/signin?session_expired=true", request.url)
    );
    
    // Clear NextAuth cookies
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
    
    log('WARN', 'Cleared stale cookies, redirecting to login');
    return response;
  }
  
  // ✅ If no session and not on public route, redirect to login
  if (!session) {
    log('WARN', 'No session found, redirecting to login', { from: pathname });
    return NextResponse.redirect(new URL("/login/signin", request.url));
  }
  
  // ✅ Check if session has error flag (from token refresh failures)
  if (session.error) {
    log('ERROR', 'Session has error flag', { 
      error: session.error,
      path: pathname
    });
    
    // Clear cookies and redirect
    const response = NextResponse.redirect(
      new URL("/login/signin?session_expired=true", request.url)
    );
    
    response.cookies.delete("next-auth.session-token");
    response.cookies.delete("__Secure-next-auth.session-token");
    response.cookies.delete("organizationSession");
    response.cookies.delete("projectSession");
    
    log('WARN', 'Cleared error session cookies, redirecting to login');
    return response;
  }
  
  // ✅ Session is valid, allow through
  log('INFO', 'Valid session, allowing request through');
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
