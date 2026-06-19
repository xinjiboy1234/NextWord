interface LoadingSkeletonProps {
  lines?: number
}

export function LoadingSkeleton({ lines = 3 }: LoadingSkeletonProps) {
  return (
    <div className="animate-pulse space-y-3">
      {Array.from({ length: lines }).map((_, index) => (
        <div key={index} className="h-4 rounded bg-neutral-200" style={{ width: `${90 - index * 10}%` }} />
      ))}
    </div>
  )
}
