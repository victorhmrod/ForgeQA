"use client";

import { useMemo, useState } from "react";

export interface ChartSeries {
  key: string;
  label: string;
  color: string;
  dashed?: boolean;
}

export interface ChartPoint {
  timestamp: string;
  [key: string]: string | number | null | undefined;
}

interface PerformanceLineChartProps {
  points: ChartPoint[];
  series: ChartSeries[];
  yFormatter?: (value: number) => string;
  referenceLines?: { value: number; label: string }[];
  height?: number;
  emptyLabel?: string;
}

const WIDTH = 640;
const PADDING = { top: 12, right: 16, bottom: 24, left: 56 };

export function PerformanceLineChart({
  points,
  series,
  yFormatter = (v) => v.toFixed(1),
  referenceLines = [],
  height = 220,
  emptyLabel = "No data.",
}: PerformanceLineChartProps) {
  const [hoverIndex, setHoverIndex] = useState<number | null>(null);

  const innerWidth = WIDTH - PADDING.left - PADDING.right;
  const innerHeight = height - PADDING.top - PADDING.bottom;

  const { minY, maxY } = useMemo(() => {
    const values: number[] = [];
    for (const point of points) {
      for (const s of series) {
        const value = point[s.key];
        if (typeof value === "number" && Number.isFinite(value)) values.push(value);
      }
    }
    for (const ref of referenceLines) values.push(ref.value);
    if (values.length === 0) return { minY: 0, maxY: 1 };
    const min = Math.min(...values, 0);
    const max = Math.max(...values);
    return max === min ? { minY: min, maxY: min + 1 } : { minY: min, maxY: max };
  }, [points, series, referenceLines]);

  if (points.length === 0) {
    return <p className="text-sm text-muted">{emptyLabel}</p>;
  }

  const xFor = (index: number) => (points.length <= 1 ? PADDING.left : PADDING.left + (index / (points.length - 1)) * innerWidth);
  const yFor = (value: number) => PADDING.top + innerHeight - ((value - minY) / (maxY - minY)) * innerHeight;

  const pathFor = (key: string) => {
    let d = "";
    let started = false;
    points.forEach((point, index) => {
      const value = point[key];
      if (typeof value !== "number" || !Number.isFinite(value)) {
        started = false;
        return;
      }
      const x = xFor(index);
      const y = yFor(value);
      d += started ? ` L ${x} ${y}` : `${d ? " " : ""}M ${x} ${y}`;
      started = true;
    });
    return d;
  };

  const yTicks = [minY, minY + (maxY - minY) / 2, maxY];
  const hovered = hoverIndex !== null ? points[hoverIndex] : null;

  function handleMouseMove(e: React.MouseEvent<SVGSVGElement>) {
    const rect = e.currentTarget.getBoundingClientRect();
    const scaleX = WIDTH / rect.width;
    const relativeX = (e.clientX - rect.left) * scaleX;
    const ratio = Math.min(1, Math.max(0, (relativeX - PADDING.left) / innerWidth));
    const index = Math.round(ratio * (points.length - 1));
    setHoverIndex(Math.min(points.length - 1, Math.max(0, index)));
  }

  return (
    <div className="relative">
      <svg
        viewBox={`0 0 ${WIDTH} ${height}`}
        className="w-full"
        onMouseMove={handleMouseMove}
        onMouseLeave={() => setHoverIndex(null)}
        role="img"
      >
        {yTicks.map((tick, i) => (
          <g key={i}>
            <line x1={PADDING.left} x2={WIDTH - PADDING.right} y1={yFor(tick)} y2={yFor(tick)} stroke="currentColor" className="text-border" strokeWidth={1} />
            <text x={PADDING.left - 8} y={yFor(tick)} textAnchor="end" dominantBaseline="middle" className="fill-current text-muted" fontSize={10}>
              {yFormatter(tick)}
            </text>
          </g>
        ))}

        {referenceLines.map((ref) => (
          <g key={ref.label}>
            <line
              x1={PADDING.left}
              x2={WIDTH - PADDING.right}
              y1={yFor(ref.value)}
              y2={yFor(ref.value)}
              stroke="currentColor"
              className="text-muted"
              strokeWidth={1}
              strokeDasharray="4 4"
            />
            <text x={WIDTH - PADDING.right} y={yFor(ref.value) - 3} textAnchor="end" className="fill-current text-muted" fontSize={9}>
              {ref.label}
            </text>
          </g>
        ))}

        {series.map((s) => (
          <path key={s.key} d={pathFor(s.key)} fill="none" stroke={s.color} strokeWidth={1.5} strokeDasharray={s.dashed ? "3 3" : undefined} />
        ))}

        {hoverIndex !== null && (
          <line x1={xFor(hoverIndex)} x2={xFor(hoverIndex)} y1={PADDING.top} y2={height - PADDING.bottom} stroke="currentColor" className="text-muted" strokeWidth={1} />
        )}
      </svg>

      <div className="mt-1 flex flex-wrap gap-3 text-xs text-muted">
        {series.map((s) => (
          <span key={s.key} className="flex items-center gap-1.5">
            <span className="inline-block h-0.5 w-3" style={{ backgroundColor: s.color }} />
            {s.label}
          </span>
        ))}
      </div>

      {hovered && (
        <div className="mt-2 rounded-md border border-border bg-surface px-3 py-2 text-xs">
          <p className="text-muted">{new Date(hovered.timestamp).toLocaleString()}</p>
          <div className="mt-1 flex flex-wrap gap-3">
            {series.map((s) => {
              const value = hovered[s.key];
              return (
                <span key={s.key}>
                  <span className="text-muted">{s.label}: </span>
                  <span style={{ color: s.color }}>{typeof value === "number" ? yFormatter(value) : "—"}</span>
                </span>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
