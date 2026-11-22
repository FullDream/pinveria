import type { Context } from 'hono'
import { resolveBackendApiUrl, resolvePublicSiteOrigin } from './server-env'

type PublicIndexMap = {
	readonly slug: string
	readonly updatedAt: string
}

type PublicIndexGame = {
	readonly slug: string
	readonly updatedAt: string
	readonly maps: readonly PublicIndexMap[]
}

type PublicIndexResponse = {
	readonly games: readonly PublicIndexGame[]
}

function escapeXml(value: string): string {
	return value
		.replace(/&/g, '&amp;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;')
		.replace(/"/g, '&quot;')
		.replace(/'/g, '&apos;')
}

function formatLastMod(iso: string | null | undefined): string | null {
	if (!iso) return null

	const parsed = new Date(iso)

	return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString()
}

export function handleRobotsTxt(context: Context): Response {
	const origin = resolvePublicSiteOrigin(context)
	const body = [
		'User-agent: *',
		'Allow: /',
		'Disallow: /*?search=*',
		'Disallow: /*&search=*',
		'Disallow: /*?sortBy=*',
		'Disallow: /*&sortBy=*',
		`Sitemap: ${origin}/sitemap.xml`,
		'',
	].join('\n')

	return context.text(body, 200, {
		'Content-Type': 'text/plain; charset=utf-8',
		'Cache-Control': 'public, max-age=3600',
	})
}

export async function handleSitemapXml(context: Context): Promise<Response> {
	const origin = resolvePublicSiteOrigin(context)
	const apiUrl = resolveBackendApiUrl()

	try {
		const response = await fetch(`${apiUrl}/public/index`, {
			headers: { Accept: 'application/json' },
			signal: AbortSignal.timeout(8_000),
		})

		if (!response.ok) {
			return context.text('Sitemap temporarily unavailable', 503, {
				'Cache-Control': 'no-store',
			})
		}

		const data = (await response.json()) as PublicIndexResponse
		const games = data.games ?? []
		const entries: { readonly loc: string; readonly lastmod: string | null }[] = []

		let latestCatalogTimestamp: number | null = null

		for (const game of games) {
			const gameTime = Date.parse(game.updatedAt)

			if (!Number.isNaN(gameTime) && (latestCatalogTimestamp === null || gameTime > latestCatalogTimestamp)) {
				latestCatalogTimestamp = gameTime
			}

			for (const map of game.maps ?? []) {
				const mapTime = Date.parse(map.updatedAt)

				if (!Number.isNaN(mapTime) && (latestCatalogTimestamp === null || mapTime > latestCatalogTimestamp)) {
					latestCatalogTimestamp = mapTime
				}
			}
		}

		entries.push({
			loc: `${origin}/`,
			lastmod: latestCatalogTimestamp !== null ? new Date(latestCatalogTimestamp).toISOString() : null,
		})

		for (const game of games) {
			entries.push({
				loc: `${origin}/games/${encodeURIComponent(game.slug)}`,
				lastmod: formatLastMod(game.updatedAt),
			})

			for (const map of game.maps ?? []) {
				entries.push({
					loc: `${origin}/games/${encodeURIComponent(game.slug)}/maps/${encodeURIComponent(map.slug)}`,
					lastmod: formatLastMod(map.updatedAt),
				})
			}
		}

		const urlElements = entries
			.map(entry => {
				const lastmodTag = entry.lastmod ? `\n    <lastmod>${escapeXml(entry.lastmod)}</lastmod>` : ''

				return `  <url>\n    <loc>${escapeXml(entry.loc)}</loc>${lastmodTag}\n  </url>`
			})
			.join('\n')

		const xml = `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urlElements}\n</urlset>\n`

		return context.body(xml, 200, {
			'Content-Type': 'application/xml; charset=utf-8',
			'Cache-Control': 'public, max-age=300',
		})
	} catch {
		return context.text('Sitemap temporarily unavailable', 503, {
			'Cache-Control': 'no-store',
		})
	}
}
