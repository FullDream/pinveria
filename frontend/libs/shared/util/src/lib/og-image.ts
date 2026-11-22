export const OG_TEMPLATE_VERSION = 'v1'

export const OG_HASH_PATTERN = /^[0-9a-f]{12}$/

export type HomeOgPayload = {
	readonly title?: string
	readonly subtitle?: string
}

export type GameOgPayload = {
	readonly name: string
	readonly developer?: string | null
	readonly mapsCount: number
	readonly markersCount: number
	readonly coverUrl: string
	readonly bannerUrl?: string | null
}

export type MapOgPayload = {
	readonly gameName: string
	readonly mapName: string
	readonly markersCount: number
	readonly categoriesCount: number
	readonly previewUrl?: string | null
	readonly gameBannerUrl?: string | null
	readonly gameCoverUrl?: string | null
}

function fnv1a32(input: string, seed: number): number {
	let hash = seed >>> 0

	for (let index = 0; index < input.length; index++) {
		hash ^= input.charCodeAt(index)
		hash = Math.imul(hash, 0x01000193) >>> 0
	}

	return hash >>> 0
}

export function hashOgPayload(parts: readonly (string | number | null | undefined)[]): string {
	const raw = [OG_TEMPLATE_VERSION, ...parts.map(part => (part == null ? '' : String(part)))].join('\u001f')
	const high = fnv1a32(raw, 0x811c9dc5)
	const low = fnv1a32(`${raw}\u0000${high}`, 0x9e3779b9)

	return `${high.toString(16).padStart(8, '0')}${(low & 0xffff).toString(16).padStart(4, '0')}`
}

export function isValidOgHash(value: string): boolean {
	return OG_HASH_PATTERN.test(value)
}

export function computeHomeOgHash(payload: HomeOgPayload = {}): string {
	return hashOgPayload(['home', payload.title ?? 'Interactive Game Maps', payload.subtitle ?? ''])
}

export function computeGameOgHash(payload: GameOgPayload): string {
	return hashOgPayload([
		'game',
		payload.name,
		payload.developer,
		payload.mapsCount,
		payload.markersCount,
		payload.coverUrl,
		payload.bannerUrl,
	])
}

export function computeMapOgHash(payload: MapOgPayload): string {
	return hashOgPayload([
		'map',
		payload.gameName,
		payload.mapName,
		payload.markersCount,
		payload.categoriesCount,
		payload.previewUrl,
		payload.gameBannerUrl,
		payload.gameCoverUrl,
	])
}

export function buildHomeOgImagePath(payload: HomeOgPayload = {}): string {
	return `/og/home/${computeHomeOgHash(payload)}.png`
}

export function buildGameOgImagePath(slug: string, payload: GameOgPayload): string {
	return `/og/games/${encodeURIComponent(slug)}/${computeGameOgHash(payload)}.png`
}

export function buildMapOgImagePath(gameSlug: string, mapSlug: string, payload: MapOgPayload): string {
	return `/og/games/${encodeURIComponent(gameSlug)}/maps/${encodeURIComponent(mapSlug)}/${computeMapOgHash(payload)}.png`
}
