export const MEDIA_IMAGE_BREAKPOINTS = [160, 320, 480, 640, 960, 1280, 1600, 1920] as const

export const MEDIA_IMAGE_SRCSET = MEDIA_IMAGE_BREAKPOINTS.map(width => `${width}w`).join(', ')

export type MediaImageFormat = 'auto' | 'avif' | 'webp' | 'jpeg' | 'png'

export type MediaImageUrlOptions = {
	width?: number
	format?: MediaImageFormat
}

const MEDIA_FILES_PREFIX = '/media/files/'
const VALID_FORMATS = new Set<string>(['auto', 'avif', 'webp', 'jpeg', 'png'])

export const isMediaImageFormat = (value: unknown): value is MediaImageFormat =>
	typeof value === 'string' && VALID_FORMATS.has(value.toLowerCase())

export const snapMediaImageWidth = (width: number): number => {
	for (const breakpoint of MEDIA_IMAGE_BREAKPOINTS) {
		if (breakpoint >= width) return breakpoint
	}

	return MEDIA_IMAGE_BREAKPOINTS[MEDIA_IMAGE_BREAKPOINTS.length - 1]
}

export const buildOptimizedMediaUrl = (src: string, options?: MediaImageUrlOptions): string => {
	if (!src.startsWith(MEDIA_FILES_PREFIX)) return src

	const [basePath, existingQuery] = src.split('?', 2)
	const params = new URLSearchParams(existingQuery ?? '')

	if (typeof options?.width === 'number' && Number.isFinite(options.width) && options.width > 0) {
		params.set('width', String(snapMediaImageWidth(options.width)))
	}

	const format = options?.format?.toLowerCase()

	params.set('format', isMediaImageFormat(format) ? format : 'auto')

	const query = params.toString()

	return query ? `${basePath}?${query}` : basePath
}
