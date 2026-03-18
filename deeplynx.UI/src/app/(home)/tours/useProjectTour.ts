import {useEffect, useRef} from "react";
import Shepherd from "shepherd.js";
import {Tour, StepOptions, PopperPlacement} from "shepherd.js";

export function useProjectTour() {
    const tourRef = useRef<Tour | null>(null);
    const builtRef = useRef(false);

    useEffect(() => {
        if (builtRef.current) return;
        builtRef.current = true;

        const tour = new Shepherd.Tour({
            useModalOverlay: true,
            defaultStepOptions: {
                cancelIcon: { enabled: true },
                scrollTo: false,
                modalOverlayOpeningPadding: 8,
                modalOverlayOpeningRadius: 8,
            },
        });

        // Theme scoping
        const addScope = () => document.body.classList.add("dlx-shepherd");
        const removeScope = () => document.body.classList.remove("dlx-shepherd");
        tour.on("start", addScope);
        tour.on("show", addScope);

        const steps: StepOptions[] = [
            {
                id: "intro",
                title: "Welcome to Your Project! 👋",
                text: "Let's take a quick tour of your project page so you can get the most out of it.",
                buttons: [
                    {
                        text: "Skip",
                        classes: "shepherd-button-secondary",
                        action: () => {
                            localStorage.setItem("project-tour-completed", "true");
                            tour.cancel();
                        },
                    },
                    { text: "Next", action: () => tour.next() },
                ],
            },
            {
                id: "project-header",
                title: "Project Overview",
                text: "This header shows your project name, description, and when it was last updated.",
                attachTo: {
                    element: "[data-tour='project-header']",
                    on: "bottom" as PopperPlacement,
                },
                scrollTo: false,
                classes: "shepherd-offset-bottom",
                buttons: [
                    {
                        text: "Back",
                        classes: "shepherd-button-secondary",
                        action: () => tour.back(),
                    },
                    { text: "Next", action: () => tour.next() },
                ],
            },
            {
                id: "search-bar",
                title: "Search the Data Catalog",
                text: "Type here to search across all records in this project. Press Enter to go directly to the data catalog with your results.",
                attachTo: {
                    element: "[data-tour='project-search']",
                    on: "bottom" as PopperPlacement,
                },
                scrollTo: false,
                classes: "shepherd-offset-bottom",
                buttons: [
                    {
                        text: "Back",
                        classes: "shepherd-button-secondary",
                        action: () => tour.back(),
                    },
                    { text: "Next", action: () => tour.next() },
                ],
            },
            {
                id: "data-catalog",
                title: "Data Catalog",
                text: "Here you can see the most recent records in this project. Click Visit to explore the full data catalog.",
                attachTo: {
                    element: "[data-tour='data-catalog-card']",
                    on: "top" as PopperPlacement,
                },
                scrollTo: { behavior: "smooth" as ScrollBehavior, block: "center" as ScrollLogicalPosition },
                classes: "shepherd-offset-top",
                buttons: [
                    {
                        text: "Back",
                        classes: "shepherd-button-secondary",
                        action: () => tour.back(),
                    },
                    { text: "Next", action: () => tour.next() },
                ],
            },
            {
                id: "widgets",
                title: "Project Widgets",
                text: "These widgets give you a quick overview of project activity, team members, and more. You can customize them to fit your needs.",
                attachTo: {
                    element: "[data-tour='project-widgets']",
                    on: "left" as PopperPlacement,
                },
                scrollTo: { behavior: "smooth" as ScrollBehavior, block: "center" as ScrollLogicalPosition },
                classes: "shepherd-offset-left",
                buttons: [
                    {
                        text: "Back",
                        classes: "shepherd-button-secondary",
                        action: () => tour.back(),
                    },
                    { text: "Next", action: () => tour.next() },
                ],
            },
            {
                id: "complete",
                title: "You're All Set! 🎉",
                text: "That's everything! You can restart this tour anytime by clicking the help button in the header.",
                buttons: [
                    {
                        text: "Finish",
                        action: () => {
                            localStorage.setItem("project-tour-completed", "true");
                            tour.complete();
                        },
                    },
                ],
            },
        ];

        steps.forEach((step) => tour.addStep(step));

        tour.on("cancel", removeScope);
        tour.on("complete", removeScope);

        tourRef.current = tour;

        const hasSeenTour= localStorage.getItem("project-tour-completed");
        if(!hasSeenTour){
            setTimeout(() => tour.start(), 500);
        }

        return () => {
            try {
                tour.cancel();
            } catch {}
            document.body.classList.remove("dlx-shepherd");
        };
    }, []);

    const startTour = () => {
        if (tourRef.current) {
            tourRef.current.start();
        }
    };

    return { startTour };
}