// auth.ts
import NextAuth from "next-auth";
import { JWT, JWTEncodeParams, JWTDecodeParams } from "next-auth/jwt";
import Okta from "next-auth/providers/okta";
import jsonWebToken from "jsonwebtoken";
import { cookies } from "next/headers";

export const runtime = "nodejs";

async function refreshAccessToken(token: JWT): Promise<JWT> {
    try {
        if (!token.refresh_token) {
            throw new Error("No refresh token available");
        }

        // Determine the correct auth server from the access token
        let authServerPath = '/oauth2'; // default for org-level auth server

        if (token.access_token) {
            try {
                const decoded = jsonWebToken.decode(token.access_token as string) as any;
                const issuer = decoded?.iss;

                if (issuer) {
                    // Extract auth server path from issuer
                    const match = issuer.match(/\/oauth2\/[^\/]+/);
                    if (match) {
                        authServerPath = match[0];
                    } else if (issuer.includes('/oauth2/')) {
                        authServerPath = '/oauth2/default';
                    } else {
                        // Root issuer - use org authorization server
                        authServerPath = '/oauth2';
                    }
                }
            } catch (e) {
                // If we can't decode, use default path
            }
        }

        // Build the token endpoint URL
        let baseUrl = process.env.OKTA_ISSUER!;
        baseUrl = baseUrl.replace(/\/oauth2\/[^\/]*\/?$/, '').replace(/\/$/, '');
        const url = `${baseUrl}${authServerPath}/v1/token`;

        const credentials = Buffer.from(
            `${process.env.OKTA_CLIENT_ID}:${process.env.OKTA_CLIENT_SECRET}`
        ).toString("base64");

        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                "Accept": "application/json",
                Authorization: `Basic ${credentials}`,
            },
            body: new URLSearchParams({
                grant_type: "refresh_token",
                refresh_token: token.refresh_token as string,
                scope: "openid profile email offline_access",
            }),
        });

        const refreshedTokens = await response.json();

        if (!response.ok) {
            throw refreshedTokens;
        }

        const newExpiresAt = Math.floor(Date.now() / 1000 + refreshedTokens.expires_in);

        return {
            ...token,
            access_token: refreshedTokens.access_token,
            id_token: refreshedTokens.id_token ?? token.id_token,
            expires_at: newExpiresAt,
            refresh_token: refreshedTokens.refresh_token ?? token.refresh_token,
            error: undefined, // Clear any previous errors
        };
    } catch (error) {
        return {
            ...token,
            error: "RefreshAccessTokenError",
        };
    }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
    providers: [
        Okta({
            clientId: process.env.OKTA_CLIENT_ID!,
            clientSecret: process.env.OKTA_CLIENT_SECRET!,
            issuer: process.env.OKTA_ISSUER,
            authorization: {
                params: {
                    scope: "openid profile email offline_access",
                    redirect_uri: process.env.REDIRECT_LINK!
                }
            },
        })
    ],
    callbacks: {
        async jwt({ token, account, profile }): Promise<JWT> {
            // Initial sign in
            if (account && profile) {

                // Read organization from cookie during sign in
                const cookieStore = await cookies();
                const orgSessionCookie = cookieStore.get("organizationSession");
                let organizationId: number | undefined;

                if (orgSessionCookie) {
                    try {
                        const orgSession = JSON.parse(orgSessionCookie.value);
                        organizationId = orgSession.organizationId;
                    } catch (e) {
                        console.error("Failed to parse org cookie:", e);
                    }
                }


                return {
                    ...token,
                    access_token: account.access_token,
                    id_token: account.id_token,
                    expires_at: account.expires_at,
                    refresh_token: account.refresh_token,
                    oktaId: profile.sub || undefined,
                    username: (profile as any).preferred_username || undefined,
                    groups: (profile as any).groups || [],
                    name: profile.name || undefined,
                    email: profile.email || undefined,
                    sub: profile.sub || undefined,
                    organizationId: organizationId,
                };
            }

            // Check if token needs refresh
            const now = Date.now();
            const expiresAt = (token.expires_at as number) * 1000;
            const timeUntilExpiry = (expiresAt - now) / 1000; // in seconds

            // Proactive refresh: refresh 5 minutes before expiry
            const BUFFER_TIME_SECONDS = 5 * 60;

            // Check if token is still valid but will expire soon
            if (now < expiresAt && timeUntilExpiry < BUFFER_TIME_SECONDS) {
                if (!token.refresh_token) {
                    return {
                        ...token,
                        error: "NoRefreshToken",
                    };
                }
                return await refreshAccessToken(token);
            }

            // Return current token if still valid
            if (now < expiresAt) {
                return token;
            }

            // Token has expired, attempt refresh
            if (!token.refresh_token) {
                return {
                    ...token,
                    error: "NoRefreshToken",
                };
            }

            return await refreshAccessToken(token);
        },

        async session({ session, token }) {
            // Handle errors
            if (token.error) {
                session.error = token.error;
            }

            // Add user information to session
            if (session.user) {
                session.user.oktaId = (token.oktaId || undefined) as string | undefined;
                session.user.username = (token.username || undefined) as string | undefined;
                session.user.groups = token.groups as string[] | undefined;
                session.user.organizationId = token.organizationId as number | undefined;
            }

            // Add tokens to session
            session.tokens = {
                access_token: token.access_token as string | undefined,
                id_token: token.id_token as string | undefined,
                expires_at: token.expires_at as number | undefined,
            };

            return session;
        },

        // auth.ts
        async redirect({ url, baseUrl }) {
            // Check if authentication is disabled
            const isAuthDisabled = process.env.NEXT_PUBLIC_DISABLE_FRONTEND_AUTHENTICATION === "true";

            // If auth is disabled, always redirect to home or the requested URL
            if (isAuthDisabled) {
                // If redirecting to a specific URL within the app, allow it
                if (url.startsWith(baseUrl)) {
                    return url;
                }
                // Otherwise go to home
                return `${baseUrl}`;
            }

            // CRITICAL: Check if this is an OAuth flow redirect
            // OAuth flows will have /api/oauth/authorize in the URL
            if (url.includes('/api/oauth/authorize')) {
                console.log(`OAuth flow detected, allowing redirect to: ${url}`);
                return url;
            }

            // If url starts with baseUrl, it's a relative URL on our site
            if (url.startsWith(baseUrl)) {
                return url;
            }

            // If url starts with /, it's a relative path
            if (url.startsWith("/")) {
                const fullUrl = `${baseUrl}${url}`;
                return fullUrl;
            }

            // For default redirect after login, check if user has an organization selected
            try {
                const cookieStore = await cookies();
                const orgSessionCookie = cookieStore.get("organizationSession");

                if (orgSessionCookie) {
                    // User has an org selected, redirect to dashboard
                    return `${baseUrl}`;
                }
            } catch (e) {
                console.error("Failed to check organization cookie:", e);
            }

            // No org selected, redirect to selection page
            return `${baseUrl}/select-org`;
        }
    },
    pages: {
        signIn: '/login/signin',
        error: '/login/error',
    },
    session: {
        strategy: "jwt",
        maxAge: 8 * 60 * 60, // 8 hours - requires login once per work day
    },
    jwt: {
        encode: async (params: JWTEncodeParams) => {
            const { secret, token } = params;
            if (!token) {
                throw new Error("Token is undefined");
            }
            const signingSecret = typeof secret === 'string' ? secret : secret[0];
            return jsonWebToken.sign(token, signingSecret);
        },
        decode: async (params: JWTDecodeParams) => {
            const { secret, token } = params;
            if (!token) {
                throw new Error("Token is undefined");
            }
            const verifyingSecret = typeof secret === 'string' ? secret : secret[0];
            return jsonWebToken.verify(token, verifyingSecret) as JWT;
        },
    },
    secret: process.env.NEXTAUTH_SECRET!,
});

// Helper functions for use in your app
export function isTokenExpired(expiresAt: number | undefined): boolean {
    if (!expiresAt) return true;
    return Date.now() >= expiresAt * 1000;
}

export function isTokenAboutToExpire(expiresAt: number | undefined, bufferMinutes: number = 5): boolean {
    if (!expiresAt) return true;
    const bufferMs = bufferMinutes * 60 * 1000;
    return Date.now() >= (expiresAt * 1000 - bufferMs);
}