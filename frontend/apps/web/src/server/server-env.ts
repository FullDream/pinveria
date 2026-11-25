import type { Context } from 'hono'

export function resolveBackendApiUrl(): string {
	const raw = process.env['PINVERIA_API_URL']?.trim() || 'http://localhost:5041'

	return raw.replace(/\/+$/, '')
}

export function resolvePublicSiteOrigin(context: Context): string {
	const configured = process.env['PUBLIC_SITE_URL']?.trim().replace(/\/+$/, '')

	if (configured) return configured

	const forwardedProto = context.req.header('x-forwarded-proto')?.split(',')[0]?.trim()
	const forwardedHost = context.req.header('x-forwarded-host')?.split(',')[0]?.trim()
	const host = forwardedHost || context.req.header('host')?.trim()

	if (host) {
		const protocol = forwardedProto || (context.req.url.startsWith('https:') ? 'https' : 'http')

		return `${protocol}://${host}`.replace(/\/+$/, '')
	}

	return new URL(context.req.url).origin.replace(/\/+$/, '')
}
