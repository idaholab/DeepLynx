import { useEffect, useState, useMemo } from "react";
import { getClass } from "@/app/lib/client_service/class_services.client";
import { GraphNodeSummary } from "../components/graphTypes";
import { useLanguage } from "@/app/contexts/Language";


export function useFilteredNodes(
    nodes: GraphNodeSummary[],
    projectId: number | undefined
) {

    const { t } = useLanguage();

    const NO_CLASS_LABEL = t.translations.NO_CLASS;

    const [archivedClassIds, setArchivedClassIds] = useState<Set<number>>(new Set());
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!projectId || nodes.length === 0) {
            setLoading(false);
            return;
        }

        let cancelled = false;

        const uniqueClassIds = Array.from(
            new Set(nodes.map((node) => node.classId).filter((id): id is number => id != null))
        );

        const checkArchivedClasses = async () => {
            const archivedSet = new Set<number>();

            await Promise.all(
                uniqueClassIds.map(async (classId) => {
                    try {
                        // getClass will throw if class is archived (hideArchived=true default)
                        await getClass(projectId, classId);
                    } catch {
                        archivedSet.add(classId);
                    }
                })
            );

            if (!cancelled) {
                setArchivedClassIds(archivedSet);
                setLoading(false);
            }
        };

        checkArchivedClasses();

        return () => {
            cancelled = true;
        };
    }, [nodes, projectId]);

    const filteredNodes = useMemo(() => {
        return nodes.map((node) => {
            if (node.classId != null && archivedClassIds.has(node.classId)) {
                return {
                    ...node,
                    classId: null,
                    className: NO_CLASS_LABEL,
                };
            }
            if (!node.className) {
                return {
                    ...node,
                    className: NO_CLASS_LABEL,
                };
            }
            return node;
        });
    }, [nodes, archivedClassIds]);

    return { filteredNodes, loading };
}
