interface ArticleTextProps {
  content: string
  activeWord?: string | null
  onWordClick: (word: string, paragraphIndex: number, paragraphText: string) => void
}

function tokenizeParagraph(text: string) {
  return text.split(/(\s+|[.,!?;:"'()—-])/).filter(Boolean)
}

function normalizeWord(word: string) {
  return word.replace(/^[^A-Za-z]+|[^A-Za-z'-]+$/g, '').toLowerCase()
}

export function ArticleText({ content, activeWord, onWordClick }: ArticleTextProps) {
  const paragraphs = content.split(/\n\s*\n/).filter(Boolean)
  const active = activeWord ? normalizeWord(activeWord) : null

  return (
    <div className="article-body">
      {paragraphs.map((paragraph, index) => (
        <p key={index}>
          {tokenizeParagraph(paragraph).map((token, tokenIndex) => {
            const isWord = /^[A-Za-z'-]+$/.test(token)
            if (!isWord) {
              return <span key={tokenIndex}>{token}</span>
            }

            const isActive = active !== null && normalizeWord(token) === active

            return (
              <button
                key={tokenIndex}
                type="button"
                onClick={() => onWordClick(token, index, paragraph)}
                className={`word-clickable${isActive ? ' active' : ''}`}
              >
                {token}
              </button>
            )
          })}
        </p>
      ))}
    </div>
  )
}
