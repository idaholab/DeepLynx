// src/app/api/oauth/authorize/route.ts
import { auth } from "../../../../../auth";
import { NextRequest, NextResponse } from "next/server";

export async function GET(request: NextRequest) {
  try {
    // Check if user is authenticated via NextAuth
    const session = await auth();

    if (!session || !session.tokens?.access_token) {
      // User not authenticated - redirect to login page
      const returnUrl = `/api/oauth/authorize${request.nextUrl.search}`;
      
      // Use NEXTAUTH_URL which is the standard for NextAuth applications
      const frontendUrl = process.env.NEXTAUTH_URL || request.nextUrl.origin;
      const loginUrl = new URL('/login/signin', frontendUrl);
      loginUrl.searchParams.set('returnUrl', returnUrl);

      return NextResponse.redirect(loginUrl);
    }

    // User is authenticated - forward request to C# backend
    const backendUrl = process.env.BACKEND_BASE_URL || "http://localhost:5095/api/v1";

    // Build the target URL with properly formatted query parameters
    const targetUrl = new URL(`${backendUrl}/oauth/authorize`);

    // Copy all query parameters from the incoming request
    request.nextUrl.searchParams.forEach((value, key) => {
      targetUrl.searchParams.set(key, value);
    });

    // Make request to C# backend with user's Okta access token
    const backendResponse = await fetch(targetUrl.toString(), {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${session.tokens.access_token}`,
        'Content-Type': 'application/json',
      },
      redirect: 'manual' // Don't automatically follow redirects
    });

    // Handle redirect responses from C# backend
    if (backendResponse.status >= 300 && backendResponse.status < 400) {
      const location = backendResponse.headers.get('Location');
      if (location) {
        // IMPORTANT: Return the redirect directly to the browser
        // NextResponse.redirect() will send a 307 redirect to the client
        return NextResponse.redirect(location, { status: 302 });
      }
    }

    // Handle error responses
    if (!backendResponse.ok) {
      const errorBody = await backendResponse.text();
      console.error(`C# backend returned error: ${backendResponse.status} - ${errorBody}`);

      try {
        const errorJson = JSON.parse(errorBody);
        return NextResponse.json(errorJson, { status: backendResponse.status });
      } catch {
        return NextResponse.json(
            { error: "server_error", error_description: "Backend request failed" },
            { status: backendResponse.status }
        );
      }
    }

    // Return successful response
    const responseBody = await backendResponse.text();
    return new NextResponse(responseBody, {
      status: backendResponse.status,
      headers: {
        'Content-Type': backendResponse.headers.get('Content-Type') || 'application/json',
      },
    });

  } catch (error) {
    console.error("Error in OAuth authorize proxy:", error);
    return NextResponse.json(
        {
          error: "server_error",
          error_description: "An unexpected error occurred in the authorization proxy"
        },
        { status: 500 }
    );
  }
}