import { useState } from 'react'
import { api } from '../api/client'
import { endpoints } from '../api/endpoints'
import type { ArticleComment } from '../types/article'

interface CommentThreadProps {
  articleId: string
  comments: ArticleComment[]
  onAdded: () => void
}

export function CommentThread({ articleId, comments, onAdded }: CommentThreadProps) {
  const [paragraphIndex, setParagraphIndex] = useState(0)
  const [paragraphText, setParagraphText] = useState('')
  const [commentText, setCommentText] = useState('')
  const [requestAiReply, setRequestAiReply] = useState(true)
  const [submitting, setSubmitting] = useState(false)

  async function submit() {
    if (!commentText.trim()) return
    setSubmitting(true)
    try {
      await api.post(endpoints.articleComments(articleId), {
        paragraphIndex,
        paragraphText,
        commentText,
        requestAiReply,
      })
      setCommentText('')
      onAdded()
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="card stack stack-md">
      <div>
        <h2 style={{ fontWeight: 540, fontSize: 'var(--text-base)' }}>段落评论</h2>
        <p style={{ marginTop: 'var(--space-1)', fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>
          点击文中单词后可在此评论该段，AI 可解释（可选）。
        </p>
      </div>

      <div className="stack stack-sm">
        <div className="field">
          <label htmlFor="comment-paragraph-index">段落索引</label>
          <input
            id="comment-paragraph-index"
            type="number"
            min={0}
            value={paragraphIndex}
            onChange={(event) => setParagraphIndex(Number(event.target.value))}
            className="input"
            autoComplete="off"
          />
        </div>
        <div className="field">
          <label htmlFor="comment-paragraph-text">段落原文（可选）</label>
          <textarea
            id="comment-paragraph-text"
            value={paragraphText}
            onChange={(event) => setParagraphText(event.target.value)}
            rows={3}
            className="textarea"
            autoComplete="off"
          />
        </div>
        <div className="field">
          <label htmlFor="comment-text">你的评论</label>
          <textarea
            id="comment-text"
            value={commentText}
            onChange={(event) => setCommentText(event.target.value)}
            rows={3}
            className="textarea"
            autoComplete="off"
          />
        </div>
        <label className="row" style={{ fontSize: 'var(--text-sm)' }}>
          <input
            type="checkbox"
            checked={requestAiReply}
            onChange={(event) => setRequestAiReply(event.target.checked)}
          />
          请求 AI 回复
        </label>
        <button
          type="button"
          onClick={() => void submit()}
          disabled={submitting}
          className="btn btn-primary"
          style={{ width: 'fit-content' }}
        >
          {submitting ? '提交中...' : '发表评论'}
        </button>
      </div>

      <div className="stack stack-sm">
        {comments.length === 0 ? (
          <p style={{ fontSize: 'var(--text-sm)', color: 'var(--muted)' }}>暂无评论。</p>
        ) : (
          comments.map((comment) => (
            <article key={comment.id} className="comment-card">
              <p className="c-meta">段落 {comment.paragraphIndex + 1}</p>
              <p className="c-body" style={{ fontWeight: 540, marginTop: 4 }}>{comment.commentText}</p>
              {comment.aiReply ? <p className="ai-reply">{comment.aiReply}</p> : null}
            </article>
          ))
        )}
      </div>
    </section>
  )
}
