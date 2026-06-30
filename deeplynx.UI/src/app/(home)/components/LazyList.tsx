import { useEffect, useRef } from "react";

interface LazyListProps {
  onReachEnd: (() => Promise<void>) | (() => void);
  children?: React.ReactNode;
}

/**
 * Signals when the last child is visible on screen.
 * This is useful for implementing a lazy loaded list.
 * `onReachEnd` is guaranteed to NOT be called more than once if it is still processing/fetching.
 */
export default function LazyList({ onReachEnd, children }: LazyListProps) {
  const sentinelRef = useRef<HTMLDivElement>(null);
  const isLoading = useRef(false);

  useEffect(() => {
    const observer = new IntersectionObserver(async ([entry]) => {
      if (entry.isIntersecting && !isLoading.current) {
        isLoading.current = true;
        await onReachEnd();
        isLoading.current = false;
      }
    });

    if (sentinelRef.current) observer.observe(sentinelRef.current);
    return () => observer.disconnect();
  }, [onReachEnd]);

  const last = (children as React.ReactNode[]).at(-1);
  const rest = (children as React.ReactNode[]).slice(0, -1);

  return (
    <>
      {rest}
      <div ref={sentinelRef} />
      {last}
    </>
  );
}
