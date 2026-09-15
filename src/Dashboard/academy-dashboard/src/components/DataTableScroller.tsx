"use client";

import {
  type ReactNode,
  useEffect,
  useRef,
  useState,
} from "react";

interface DataTableScrollerProps {
  children: ReactNode;
  className?: string;
}

export default function DataTableScroller({
  children,
  className = "",
}: DataTableScrollerProps) {
  const topScrollRef =
    useRef<HTMLDivElement | null>(null);

  const viewportRef =
    useRef<HTMLDivElement | null>(null);

  const [scrollWidth, setScrollWidth] =
    useState(0);

  const [hasOverflow, setHasOverflow] =
    useState(false);

  useEffect(() => {
    const topScroll =
      topScrollRef.current;

    const viewport =
      viewportRef.current;

    if (!topScroll || !viewport) {
      return;
    }

    let frameId: number | null =
      null;

    let syncing = false;

    const measure = () => {
      if (frameId !== null) {
        window.cancelAnimationFrame(
          frameId
        );
      }

      frameId =
        window.requestAnimationFrame(
          () => {
            frameId = null;

            const width =
              viewport.scrollWidth;

            setScrollWidth(width);

            setHasOverflow(
              width >
                viewport.clientWidth +
                  1
            );

            topScroll.scrollLeft =
              viewport.scrollLeft;
          }
        );
    };

    const syncFromTop = () => {
      if (syncing) {
        return;
      }

      syncing = true;

      viewport.scrollLeft =
        topScroll.scrollLeft;

      window.requestAnimationFrame(
        () => {
          syncing = false;
        }
      );
    };

    const syncFromViewport = () => {
      if (syncing) {
        return;
      }

      syncing = true;

      topScroll.scrollLeft =
        viewport.scrollLeft;

      window.requestAnimationFrame(
        () => {
          syncing = false;
        }
      );
    };

    topScroll.addEventListener(
      "scroll",
      syncFromTop,
      {
        passive: true,
      }
    );

    viewport.addEventListener(
      "scroll",
      syncFromViewport,
      {
        passive: true,
      }
    );

    let resizeObserver:
      ResizeObserver | null =
        null;

    if (
      typeof ResizeObserver !==
      "undefined"
    ) {
      resizeObserver =
        new ResizeObserver(
          measure
        );

      resizeObserver.observe(
        viewport
      );

      const table =
        viewport.querySelector(
          "table"
        );

      if (table) {
        resizeObserver.observe(
          table
        );
      }
    }

    window.addEventListener(
      "resize",
      measure
    );

    measure();

    return () => {
      if (frameId !== null) {
        window.cancelAnimationFrame(
          frameId
        );
      }

      topScroll.removeEventListener(
        "scroll",
        syncFromTop
      );

      viewport.removeEventListener(
        "scroll",
        syncFromViewport
      );

      window.removeEventListener(
        "resize",
        measure
      );

      resizeObserver?.disconnect();
    };
  }, []);

  return (
    <div className="data-table-shell">
      <div
        ref={topScrollRef}
        className={
          "data-table-top-scroll " +
          (hasOverflow
            ? ""
            : "data-table-top-scroll-hidden")
        }
        title={
          hasOverflow
            ? "Scroll table horizontally"
            : undefined
        }
        aria-hidden="true"
      >
        <div
          className="data-table-top-scroll-spacer"
          style={{
            width: scrollWidth,
          }}
        />
      </div>

      <div
        ref={viewportRef}
        className={
          "data-table-scroll-viewport " +
          className
        }
      >
        {children}
      </div>
    </div>
  );
}