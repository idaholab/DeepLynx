// src/app/contexts/ProjectSessionProvider.tsx
"use client";

import React, {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
} from "react";
import type { ReactNode, Context } from "react";

export interface ProjectSession {
  projectId: string | number;
  projectName: string;
}

interface ProjectSessionContextType {
  project: ProjectSession | null;
  setProject: (project: ProjectSession) => void;
  clearProject: () => void;
  hasLoaded: boolean;
}

const ProjectSessionContext: Context<ProjectSessionContextType | undefined> =
  createContext<ProjectSessionContextType | undefined>(undefined);

export const useProjectSession = (): ProjectSessionContextType => {
  const context = useContext(ProjectSessionContext);
  if (!context) {
    throw new Error(
      "useProjectSession must be used within a ProjectSessionProvider"
    );
  }
  return context;
};

export const ProjectSessionProvider = ({
  children,
}: {
  children: ReactNode;
}): React.JSX.Element => {
  const [project, setProjectState] = useState<ProjectSession | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);

  useEffect(() => {
    const storedLocal = localStorage.getItem("projectSession");
    const storedCookie = document.cookie
      .split("; ")
      .find((row) => row.startsWith("projectSession="))
      ?.split("=")[1];

    const stored =
      storedLocal || (storedCookie ? decodeURIComponent(storedCookie) : null);

    if (stored) {
      try {
        const parsed: ProjectSession | null = JSON.parse(stored);
        if (parsed && parsed.projectId) {
          setProjectState(parsed);
        }
      } catch {
        console.warn("Failed to parse stored project session.");
      }
    }

    setHasLoaded(true);
  }, []);

  const setProject = useCallback((proj: ProjectSession) => {
    setProjectState(proj);
    const serialized = JSON.stringify(proj);

    localStorage.setItem("projectSession", serialized);

    const maxAge = 30 * 24 * 60 * 60;
    document.cookie = `projectSession=${encodeURIComponent(
      serialized
    )}; path=/; max-age=${maxAge}; SameSite=Lax`;
  }, []);

  const clearProject = useCallback(() => {
    console.log("ProjectSessionProvider clearProject called");
    setProjectState(null);
    localStorage.removeItem("projectSession");
    document.cookie = "projectSession=; path=/; max-age=0";
  }, []);

  return (
    <ProjectSessionContext.Provider
      value={{ project, setProject, clearProject, hasLoaded }}
    >
      {children}
    </ProjectSessionContext.Provider>
  );
};
