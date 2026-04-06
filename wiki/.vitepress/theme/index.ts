import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import { onMounted, watch, nextTick } from 'vue'
import { useRoute } from 'vitepress'
import mediumZoom from 'medium-zoom'
import './custom.css'

export default {
  extends: DefaultTheme,
  setup() {
    const route = useRoute()

    const initZoom = () => {
      mediumZoom('.vp-doc img:not(.no-zoom)', {
        background: 'rgba(0, 0, 0, 0.92)',
      })
    }

    const initMermaidZoom = (retries = 20) => {
      const mermaidSvgs = document.querySelectorAll('.mermaid svg')
      if (mermaidSvgs.length === 0 && retries > 0) {
        setTimeout(() => initMermaidZoom(retries - 1), 500)
        return
      }

      mermaidSvgs.forEach((svg) => {
        if (svg.getAttribute('data-zoom-initialized')) return
        svg.setAttribute('data-zoom-initialized', 'true')

        const container = svg.closest('.mermaid') as HTMLElement
        if (container) {
          container.style.cursor = 'pointer'
          container.setAttribute('title', '🔍 Click to zoom')
        }

        svg.addEventListener('click', () => {
          const overlay = document.createElement('div')
          overlay.className = 'mermaid-zoom-overlay'

          const controls = document.createElement('div')
          controls.className = 'mermaid-zoom-controls'
          controls.innerHTML = `
            <button class="zoom-btn" data-action="in">+</button>
            <button class="zoom-btn" data-action="out">−</button>
            <button class="zoom-btn" data-action="reset">Reset</button>
            <button class="zoom-btn zoom-close" data-action="close">✕</button>
          `

          const content = document.createElement('div')
          content.className = 'mermaid-zoom-content'

          const cloned = svg.cloneNode(true) as SVGElement
          cloned.removeAttribute('data-zoom-initialized')
          cloned.style.maxWidth = '100%'
          cloned.style.maxHeight = '85vh'
          cloned.style.width = 'auto'
          cloned.style.height = 'auto'

          if (!cloned.getAttribute('viewBox')) {
            const bbox = (svg as SVGSVGElement).getBBox?.()
            if (bbox) {
              cloned.setAttribute(
                'viewBox',
                `${bbox.x} ${bbox.y} ${bbox.width} ${bbox.height}`
              )
            }
          }

          content.appendChild(cloned)
          overlay.appendChild(controls)
          overlay.appendChild(content)
          document.body.appendChild(overlay)
          document.body.style.overflow = 'hidden'

          let scale = 1
          let translateX = 0
          let translateY = 0

          const updateTransform = () => {
            content.style.transform = `translate(${translateX}px, ${translateY}px) scale(${scale})`
          }

          controls.addEventListener('click', (e) => {
            const target = e.target as HTMLElement
            const action = target.getAttribute('data-action')
            if (action === 'in') {
              scale = Math.min(scale * 1.3, 5)
              updateTransform()
            } else if (action === 'out') {
              scale = Math.max(scale / 1.3, 0.2)
              updateTransform()
            } else if (action === 'reset') {
              scale = 1
              translateX = 0
              translateY = 0
              updateTransform()
            } else if (action === 'close') {
              close()
            }
          })

          const close = () => {
            overlay.remove()
            document.body.style.overflow = ''
          }

          overlay.addEventListener('click', (e) => {
            if (e.target === overlay) close()
          })

          content.addEventListener(
            'wheel',
            (e) => {
              e.preventDefault()
              const delta = e.deltaY > 0 ? 0.9 : 1.1
              scale = Math.min(Math.max(scale * delta, 0.2), 5)
              updateTransform()
            },
            { passive: false }
          )

          let isPanning = false
          let startX = 0
          let startY = 0

          content.addEventListener('mousedown', (e) => {
            isPanning = true
            startX = e.clientX - translateX
            startY = e.clientY - translateY
            content.style.cursor = 'grabbing'
          })

          document.addEventListener('mousemove', (e) => {
            if (!isPanning) return
            translateX = e.clientX - startX
            translateY = e.clientY - startY
            updateTransform()
          })

          document.addEventListener('mouseup', () => {
            isPanning = false
            content.style.cursor = 'grab'
          })

          const handleKey = (e: KeyboardEvent) => {
            if (e.key === 'Escape') close()
            if (e.key === '+' || e.key === '=') {
              scale = Math.min(scale * 1.3, 5)
              updateTransform()
            }
            if (e.key === '-') {
              scale = Math.max(scale / 1.3, 0.2)
              updateTransform()
            }
            if (e.key === '0') {
              scale = 1
              translateX = 0
              translateY = 0
              updateTransform()
            }
          }

          document.addEventListener('keydown', handleKey)
          overlay.addEventListener('remove', () => {
            document.removeEventListener('keydown', handleKey)
          })
        })
      })
    }

    onMounted(() => {
      initZoom()
      initMermaidZoom()
    })

    watch(
      () => route.path,
      () =>
        nextTick(() => {
          initZoom()
          initMermaidZoom()
        })
    )
  },
} satisfies Theme
