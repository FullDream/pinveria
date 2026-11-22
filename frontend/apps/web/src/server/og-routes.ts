import {
	buildGameOgImagePath,
	buildHomeOgImagePath,
	buildMapOgImagePath,
	computeGameOgHash,
	computeHomeOgHash,
	computeMapOgHash,
	formatCount,
	isValidOgHash,
} from '@game-atlas/shared/util'
import { Resvg } from '@resvg/resvg-js'
import type { Context } from 'hono'
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import satori, { type Font } from 'satori'
import { html } from 'satori-html'
import sharp from 'sharp'
import { resolveBackendApiUrl } from './server-env'

type PublicImageAsset = {
	readonly url: string
}

type PublicGameMapSummary = {
	readonly slug: string
	readonly name: string
	readonly preview: PublicImageAsset | null
	readonly markersCount: number
	readonly categoriesCount: number
}

type PublicGameDetails = {
	readonly name: string
	readonly slug: string
	readonly developer: string | null
	readonly cover: PublicImageAsset
	readonly banner: PublicImageAsset | null
	readonly mapsCount: number
	readonly markersCount: number
	readonly categoriesCount: number
	readonly maps: readonly PublicGameMapSummary[]
}

type PublicMapStyleSummary = {
	readonly id: string
	readonly preview: PublicImageAsset
}

type PublicMapCategoryGroupSummary = {
	readonly categories: readonly unknown[]
}

type PublicMapDetailsResponse = {
	readonly name: string
	readonly slug: string
	readonly defaultStyleId: string | null
	readonly styles: readonly PublicMapStyleSummary[]
	readonly categoryGroups: readonly PublicMapCategoryGroupSummary[]
	readonly markers: readonly unknown[]
}

const OG_WIDTH = 1200
const OG_HEIGHT = 630

let cachedFonts: Font[] | null = null
let cachedHeroDataUri: string | null | undefined

function escapeHtml(value: string): string {
	return value
		.replace(/&/g, '&amp;')
		.replace(/</g, '&lt;')
		.replace(/>/g, '&gt;')
		.replace(/"/g, '&quot;')
		.replace(/'/g, '&#39;')
}

function truncateText(value: string, maxLength: number): string {
	const cleaned = value.replace(/\s+/g, ' ').trim()

	if (cleaned.length <= maxLength) return cleaned

	return `${cleaned.slice(0, maxLength - 1).trimEnd()}…`
}

function parseHashSegment(hashPng: string): string | null {
	if (!hashPng.endsWith('.png')) return null

	const hash = hashPng.slice(0, -4)

	return isValidOgHash(hash) ? hash : null
}

function resolveFontPaths(): { readonly regular: string; readonly bold: string } | null {
	const candidates = [
		{
			regular: resolve(process.cwd(), 'node_modules/@fontsource/inter/files/inter-latin-400-normal.woff'),
			bold: resolve(process.cwd(), 'node_modules/@fontsource/inter/files/inter-latin-700-normal.woff'),
		},
		{
			regular: resolve(process.cwd(), '../node_modules/@fontsource/inter/files/inter-latin-400-normal.woff'),
			bold: resolve(process.cwd(), '../node_modules/@fontsource/inter/files/inter-latin-700-normal.woff'),
		},
		{
			regular: 'C:\\Windows\\Fonts\\segoeui.ttf',
			bold: 'C:\\Windows\\Fonts\\segoeuib.ttf',
		},
		{
			regular: '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',
			bold: '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
		},
	]

	for (const candidate of candidates) {
		if (existsSync(candidate.regular) && existsSync(candidate.bold)) {
			return candidate
		}
	}

	return null
}

function loadFonts(): Font[] {
	if (cachedFonts) return cachedFonts

	const paths = resolveFontPaths()

	if (!paths) {
		throw new Error('Could not locate fonts for OG image rendering.')
	}

	const regularBuffer = readFileSync(paths.regular)
	const boldBuffer = readFileSync(paths.bold)

	cachedFonts = [
		{
			name: 'Inter',
			data: regularBuffer,
			weight: 400,
			style: 'normal',
		},
		{
			name: 'Inter',
			data: boldBuffer,
			weight: 700,
			style: 'normal',
		},
	]

	return cachedFonts
}

async function normalizeBufferToDataUri(
	buffer: Buffer,
	width: number,
	height: number,
	format: 'jpeg' | 'png' = 'jpeg',
): Promise<string | null> {
	try {
		const pipeline = sharp(buffer).resize(width, height, { fit: 'cover', position: 'center' })
		const output =
			format === 'png' ? await pipeline.png().toBuffer() : await pipeline.jpeg({ quality: 84 }).toBuffer()
		const mime = format === 'png' ? 'image/png' : 'image/jpeg'

		return `data:${mime};base64,${output.toString('base64')}`
	} catch {
		return null
	}
}

async function loadHeroBackdropDataUri(browserDistFolder: string): Promise<string | null> {
	if (cachedHeroDataUri !== undefined) return cachedHeroDataUri

	const candidates = [
		resolve(browserDistFolder, 'hero-atlas.webp'),
		resolve(process.cwd(), 'apps/web/public/hero-atlas.webp'),
		resolve(process.cwd(), 'dist/apps/web/browser/hero-atlas.webp'),
	]

	for (const candidate of candidates) {
		if (existsSync(candidate)) {
			const raw = readFileSync(candidate)

			cachedHeroDataUri = await normalizeBufferToDataUri(raw, OG_WIDTH, OG_HEIGHT, 'jpeg')

			return cachedHeroDataUri
		}
	}

	cachedHeroDataUri = null

	return null
}

async function fetchBackendImageDataUri(
	pathOrUrl: string | null | undefined,
	width: number,
	height: number,
	format: 'jpeg' | 'png' = 'jpeg',
): Promise<string | null> {
	if (!pathOrUrl) return null

	const apiUrl = resolveBackendApiUrl()
	const targetUrl = /^https?:\/\//i.test(pathOrUrl)
		? pathOrUrl
		: `${apiUrl}${pathOrUrl.startsWith('/') ? pathOrUrl : `/${pathOrUrl}`}`

	try {
		const response = await fetch(targetUrl, { signal: AbortSignal.timeout(6_000) })

		if (!response.ok) return null

		const arrayBuffer = await response.arrayBuffer()

		return await normalizeBufferToDataUri(Buffer.from(arrayBuffer), width, height, format)
	} catch {
		return null
	}
}

async function renderMarkupToPng(markup: string): Promise<Uint8Array> {
	const vnode = html(markup)
	const svg = await satori(vnode as Parameters<typeof satori>[0], {
		width: OG_WIDTH,
		height: OG_HEIGHT,
		fonts: loadFonts(),
	})

	const resvg = new Resvg(svg, {
		fitTo: { mode: 'width', value: OG_WIDTH },
	})

	return resvg.render().asPng()
}

const BRAND_LOGO_DATA_URI = `data:image/svg+xml;base64,${Buffer.from(
	`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="64" height="64" fill="#fafafa"><g><path fill-rule="evenodd" d="M 256 22 L 286 114 L 378 144 L 286 174 L 256 266 L 226 174 L 134 144 L 226 114 Z M 256 48 L 234 122 L 160 144 L 234 166 L 256 240 L 278 166 L 352 144 L 278 122 Z" /><polygon points="256,42 280,120 256,144" /><polygon points="358,144 280,168 256,144" /><polygon points="256,246 232,168 256,144" /><polygon points="154,144 232,120 256,144" /></g><path fill-rule="evenodd" d="M 30 130 C 22 128, 18 135, 22 143 L 226 472 C 239 492, 273 492, 286 472 L 490 143 C 494 135, 490 128, 482 130 L 395 153 C 387 155, 381 161, 376 169 L 256 372 L 136 169 C 131 161, 125 155, 117 153 Z M 92 174 L 106 182 L 256 434 L 406 182 L 420 174 L 256 454 Z" /></svg>`,
).toString('base64')}`

function buildBrandHeaderHtml(subtitle?: string): string {
	const subtitleMarkup = subtitle
		? `<span style="color: #a1a1aa; font-size: 24px; font-weight: 400; line-height: 1; margin-left: 14px;">· ${escapeHtml(subtitle)}</span>`
		: ''

	return `
		<div style="display: flex; align-items: center;">
			<img src="${BRAND_LOGO_DATA_URI}" width="64" height="64" style="width: 64px; height: 64px;" />
			<div style="display: flex; align-items: center; margin-left: 18px;">
				<span style="color: #fafafa; font-size: 36px; font-weight: 700; line-height: 1; letter-spacing: -0.02em;">GameAtlas</span>
				${subtitleMarkup}
			</div>
		</div>
	`
}

function buildPillHtml(label: string): string {
	return `
		<div style="display: flex; align-items: center; padding: 12px 24px; border-radius: 9999px; background-color: rgba(255, 255, 255, 0.14); border: 1px solid rgba(255, 255, 255, 0.18); color: #fafafa; font-size: 22px; font-weight: 700; margin-right: 16px;">
			${escapeHtml(label)}
		</div>
	`
}

export async function handleHomeOgImage(context: Context, browserDistFolder: string): Promise<Response> {
	const requestedHash = parseHashSegment(context.req.param('hashPng') ?? '')

	if (!requestedHash) {
		return context.notFound()
	}

	const currentHash = computeHomeOgHash()

	if (requestedHash !== currentHash) {
		context.header('Cache-Control', 'no-store')

		return context.redirect(buildHomeOgImagePath(), 302)
	}

	const backdropUri = await loadHeroBackdropDataUri(browserDistFolder)
	const backdropImg = backdropUri
		? `<img src="${backdropUri}" width="1200" height="630" style="position: absolute; top: 0; left: 0; width: 1200px; height: 630px; object-fit: cover;" />`
		: ''

	const markup = `
		<div style="display: flex; position: relative; width: 1200px; height: 630px; background-color: #09090b; font-family: Inter; overflow: hidden;">
			${backdropImg}
			<div style="display: flex; position: absolute; top: 0; left: 0; width: 1200px; height: 630px; background-image: linear-gradient(90deg, rgba(9,9,11,0.96) 0%, rgba(9,9,11,0.86) 58%, rgba(9,9,11,0.45) 100%);"></div>
			<div style="display: flex; flex-direction: column; justify-content: space-between; position: relative; width: 1200px; height: 630px; padding: 64px 72px;">
				${buildBrandHeaderHtml()}
				<div style="display: flex; flex-direction: column; max-width: 820px;">
					<div style="color: #fafafa; font-size: 68px; font-weight: 700; line-height: 1.08; letter-spacing: -0.03em;">
						Interactive Game Maps
					</div>
					<div style="margin-top: 22px; color: #d4d4d8; font-size: 30px; font-weight: 400; line-height: 1.4;">
						Explore detailed game worlds with collectibles, quests, bosses, and hidden locations.
					</div>
				</div>
				<div style="display: flex; align-items: center;">
					${buildPillHtml('Unlimited markers')}
					${buildPillHtml('Multiple maps')}
				</div>
			</div>
		</div>
	`

	const png = await renderMarkupToPng(markup)

	return context.body(png as unknown as ArrayBuffer, 200, {
		'Content-Type': 'image/png',
		'Cache-Control': 'public, max-age=31536000, immutable',
	})
}

export async function handleGameOgImage(context: Context): Promise<Response> {
	const slug = context.req.param('slug') ?? ''
	const requestedHash = parseHashSegment(context.req.param('hashPng') ?? '')

	if (!slug || !requestedHash) {
		return context.notFound()
	}

	const apiUrl = resolveBackendApiUrl()

	try {
		const response = await fetch(`${apiUrl}/public/games/${encodeURIComponent(slug)}`, {
			headers: { Accept: 'application/json' },
			signal: AbortSignal.timeout(6_000),
		})

		if (response.status === 404) {
			return context.notFound()
		}

		if (!response.ok) {
			return context.text('Upstream error', 503, { 'Cache-Control': 'no-store' })
		}

		const game = (await response.json()) as PublicGameDetails
		const payload = {
			name: game.name,
			developer: game.developer,
			mapsCount: game.mapsCount,
			markersCount: game.markersCount,
			coverUrl: game.cover.url,
			bannerUrl: game.banner?.url ?? null,
		}
		const currentHash = computeGameOgHash(payload)

		if (requestedHash !== currentHash) {
			context.header('Cache-Control', 'no-store')

			return context.redirect(buildGameOgImagePath(game.slug, payload), 302)
		}

		const [backdropUri, coverUri] = await Promise.all([
			fetchBackendImageDataUri(game.banner?.url ?? game.cover.url, OG_WIDTH, OG_HEIGHT, 'jpeg'),
			fetchBackendImageDataUri(game.cover.url, 220, 280, 'png'),
		])

		const backdropImg = backdropUri
			? `<img src="${backdropUri}" width="1200" height="630" style="position: absolute; top: 0; left: 0; width: 1200px; height: 630px; object-fit: cover;" />`
			: ''

		const coverBlock = coverUri
			? `<img src="${coverUri}" width="220" height="280" style="width: 220px; height: 280px; border-radius: 24px; border: 6px solid rgba(255,255,255,0.9); object-fit: cover; margin-right: 48px;" />`
			: ''

		const developerLine = game.developer
			? `<div style="margin-top: 12px; color: #d4d4d8; font-size: 26px; font-weight: 400;">${escapeHtml(truncateText(game.developer, 60))}</div>`
			: ''

		const markup = `
			<div style="display: flex; position: relative; width: 1200px; height: 630px; background-color: #09090b; font-family: Inter; overflow: hidden;">
				${backdropImg}
				<div style="display: flex; position: absolute; top: 0; left: 0; width: 1200px; height: 630px; background-image: linear-gradient(90deg, rgba(9,9,11,0.95) 0%, rgba(9,9,11,0.85) 62%, rgba(9,9,11,0.45) 100%);"></div>
				<div style="display: flex; flex-direction: column; justify-content: space-between; position: relative; width: 1200px; height: 630px; padding: 60px 72px;">
					${buildBrandHeaderHtml('Interactive Game Atlas')}
					<div style="display: flex; align-items: center;">
						${coverBlock}
						<div style="display: flex; flex-direction: column; max-width: 780px;">
							<div style="color: #fafafa; font-size: 62px; font-weight: 700; line-height: 1.08; letter-spacing: -0.03em;">
								${escapeHtml(truncateText(game.name, 52))}
							</div>
							${developerLine}
							<div style="display: flex; align-items: center; margin-top: 32px;">
								${buildPillHtml(formatCount(game.mapsCount, 'map'))}
								${buildPillHtml(formatCount(game.markersCount, 'marker'))}
							</div>
						</div>
					</div>
					<div style="display: flex; color: #a1a1aa; font-size: 20px;">
						Interactive maps, collectibles, quests, and locations
					</div>
				</div>
			</div>
		`

		const png = await renderMarkupToPng(markup)

		return context.body(png as unknown as ArrayBuffer, 200, {
			'Content-Type': 'image/png',
			'Cache-Control': 'public, max-age=31536000, immutable',
		})
	} catch {
		return context.text('Error rendering OG image', 503, { 'Cache-Control': 'no-store' })
	}
}

export async function handleMapOgImage(context: Context): Promise<Response> {
	const slug = context.req.param('slug') ?? ''
	const mapSlug = context.req.param('mapSlug') ?? ''
	const requestedHash = parseHashSegment(context.req.param('hashPng') ?? '')

	if (!slug || !mapSlug || !requestedHash) {
		return context.notFound()
	}

	const apiUrl = resolveBackendApiUrl()

	try {
		const [gameRes, mapRes] = await Promise.all([
			fetch(`${apiUrl}/public/games/${encodeURIComponent(slug)}`, {
				headers: { Accept: 'application/json' },
				signal: AbortSignal.timeout(6_000),
			}),
			fetch(`${apiUrl}/public/games/${encodeURIComponent(slug)}/maps/${encodeURIComponent(mapSlug)}`, {
				headers: { Accept: 'application/json' },
				signal: AbortSignal.timeout(6_000),
			}),
		])

		if (gameRes.status === 404 || mapRes.status === 404) {
			return context.notFound()
		}

		if (!gameRes.ok || !mapRes.ok) {
			return context.text('Upstream error', 503, { 'Cache-Control': 'no-store' })
		}

		const game = (await gameRes.json()) as PublicGameDetails
		const map = (await mapRes.json()) as PublicMapDetailsResponse
		const categoriesCount = (map.categoryGroups ?? []).reduce(
			(sum, group) => sum + (group.categories?.length ?? 0),
			0,
		)
		const markersCount = map.markers?.length ?? 0
		const selectedStyle =
			(map.styles ?? []).find(style => style.id === map.defaultStyleId) ?? map.styles?.[0] ?? null
		const summaryPreview = (game.maps ?? []).find(item => item.slug === map.slug)?.preview?.url ?? null
		const previewUrl = summaryPreview ?? selectedStyle?.preview.url ?? null

		const payload = {
			gameName: game.name,
			mapName: map.name,
			markersCount,
			categoriesCount,
			previewUrl,
			gameBannerUrl: game.banner?.url ?? null,
			gameCoverUrl: game.cover.url,
		}
		const currentHash = computeMapOgHash(payload)

		if (requestedHash !== currentHash) {
			context.header('Cache-Control', 'no-store')

			return context.redirect(buildMapOgImagePath(game.slug, map.slug, payload), 302)
		}

		const backdropSource = previewUrl ?? game.banner?.url ?? game.cover.url
		const backdropUri = await fetchBackendImageDataUri(backdropSource, OG_WIDTH, OG_HEIGHT, 'jpeg')
		const backdropImg = backdropUri
			? `<img src="${backdropUri}" width="1200" height="630" style="position: absolute; top: 0; left: 0; width: 1200px; height: 630px; object-fit: cover;" />`
			: ''

		const markup = `
			<div style="display: flex; position: relative; width: 1200px; height: 630px; background-color: #09090b; font-family: Inter; overflow: hidden;">
				${backdropImg}
				<div style="display: flex; position: absolute; top: 0; left: 0; width: 1200px; height: 630px; background-image: linear-gradient(90deg, rgba(9,9,11,0.95) 0%, rgba(9,9,11,0.85) 60%, rgba(9,9,11,0.42) 100%);"></div>
				<div style="display: flex; flex-direction: column; justify-content: space-between; position: relative; width: 1200px; height: 630px; padding: 60px 72px;">
					${buildBrandHeaderHtml(truncateText(game.name, 48))}
					<div style="display: flex; flex-direction: column; max-width: 960px;">
						<div style="color: #a1a1aa; font-size: 24px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em;">
							Interactive Map
						</div>
						<div style="margin-top: 12px; color: #fafafa; font-size: 66px; font-weight: 700; line-height: 1.08; letter-spacing: -0.03em;">
							${escapeHtml(truncateText(map.name, 56))}
						</div>
						<div style="margin-top: 18px; color: #d4d4d8; font-size: 28px; font-weight: 400;">
							${escapeHtml(truncateText(game.name, 64))}
						</div>
					</div>
					<div style="display: flex; align-items: center;">
						${buildPillHtml(formatCount(markersCount, 'marker'))}
						${buildPillHtml(formatCount(categoriesCount, 'category', 'categories'))}
					</div>
				</div>
			</div>
		`

		const png = await renderMarkupToPng(markup)

		return context.body(png as unknown as ArrayBuffer, 200, {
			'Content-Type': 'image/png',
			'Cache-Control': 'public, max-age=31536000, immutable',
		})
	} catch {
		return context.text('Error rendering OG image', 503, { 'Cache-Control': 'no-store' })
	}
}
