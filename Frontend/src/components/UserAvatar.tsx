interface UserAvatarProps {
  displayName: string
  active?: boolean
  onClick?: () => void
}

function getInitials(name: string) {
  const trimmed = name.trim()
  if (!trimmed) return '?'
  const parts = trimmed.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) {
    return `${parts[0][0]}${parts[1][0]}`.toUpperCase()
  }
  return trimmed.slice(0, 2).toUpperCase()
}

export function UserAvatar({ displayName, active = false, onClick }: UserAvatarProps) {
  const initials = getInitials(displayName)

  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="我的"
      title={displayName}
      className={`grid h-11 w-11 place-items-center rounded-full border-2 text-sm font-semibold transition ${
        active
          ? 'border-emerald-700 bg-emerald-700 text-white'
          : 'border-emerald-200 bg-emerald-50 text-emerald-800 hover:border-emerald-400 hover:bg-emerald-100'
      }`}
    >
      {initials}
    </button>
  )
}
