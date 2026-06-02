// src/app/layout.tsx (Server Component)
import "./globals.css";
import "react-loading-skeleton/dist/skeleton.css";
import "shepherd.js/dist/css/shepherd.css";
import "../../styles/shepherd-theme.css";
import ClientProviders from "./contexts/ClientProviders";

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" data-theme="default" suppressHydrationWarning>
      <head>
        <script
          dangerouslySetInnerHTML={{
            __html: `
	(function () {
	  try {
	    var KEY = 'dlx-theme-mode';
	    var saved = localStorage.getItem(KEY);
	    var orgTheme = 'default';
	    var storedOrg = localStorage.getItem('organizationSession');

	    if (storedOrg) {
	      var parsedOrg = JSON.parse(storedOrg);
	      if (['default', 'nric', 'nord'].indexOf(parsedOrg.themeName) >= 0) {
	        orgTheme = parsedOrg.themeName;
	      }
	    }

	    document.documentElement.setAttribute('data-theme', saved === 'dark' ? orgTheme + '-dark' : orgTheme);
	  } catch (e) {}
	})();
	          `,
          }}
        />
      </head>
      <body className="min-h-screen bg-base-100 text-base-content">
        <ClientProviders>{children}</ClientProviders>
      </body>
    </html>
  );
}
