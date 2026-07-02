interface LoadingSkeletonProps {
  lines?: number
}

export function LoadingSkeleton({ lines = 3 }: LoadingSkeletonProps) {
  return (
    <div className="stack stack-sm">
      {Array.from({ length: lines }).map((_, index) => (
        <div
          key={index}
          className="skeleton skeleton-text"
          style={{ width: `${90 - index * 10}%`, height: 14 }}
        />
      ))}
    </div>
  )
}
