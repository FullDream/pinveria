import { AngularAppEngine, createRequestHandler } from '@angular/ssr'
import { isMainModule } from '@angular/ssr/node'
import { serve } from '@hono/node-server'
import { serveStatic } from '@hono/node-server/serve-static'
import { Hono } from 'hono'
import { existsSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { handleGameOgImage, handleHomeOgImage, handleMapOgImage } from './server/og-routes'
import { handleRobotsTxt, handleSitemapXml } from './server/seo-routes'

const HASHED_ASSET_PATTERN = /-[A-Za-z0-9_-]{8}\.[^/]+$/

const serverDistFolder = dirname(fileURLToPath(import.meta.url))
const browserDistFolder = resolve(serverDistFolder, '../browser')

const envAllowedHosts = process.env['NG_ALLOWED_HOSTS']
	?.split(',')
	.map(host => host.trim())
	.filter(Boolean)

const angularApp = new AngularAppEngine({
	allowedHosts: envAllowedHosts?.length ? envAllowedHosts : ['localhost', '127.0.0.1'],
	trustProxyHeaders: true,
})

const app = new Hono()

app.get('/healthz', c => c.json({ status: 'ok' }))
app.get('/robots.txt', c => handleRobotsTxt(c))
app.get('/sitemap.xml', c => handleSitemapXml(c))
app.get('/og/home/:hashPng', c => handleHomeOgImage(c, browserDistFolder))
app.get('/og/games/:slug/:hashPng', c => handleGameOgImage(c))
app.get('/og/games/:slug/maps/:mapSlug/:hashPng', c => handleMapOgImage(c))

if (existsSync(browserDistFolder)) {
	const staticHandler = serveStatic({
		root: browserDistFolder,
		onFound: (filePath, c) => {
			if (HASHED_ASSET_PATTERN.test(filePath)) {
				c.header('Cache-Control', 'public, max-age=31536000, immutable')
			}
		},
	})

	app.use('*', async (c, next) => {
		const path = c.req.path

		if (path === '/' || path.endsWith('/') || path === '/index.html' || path === '/index.csr.html') {
			return next()
		}

		return staticHandler(c, next)
	})
}

app.all('*', async c => {
	const response = await angularApp.handle(c.req.raw)

	return response ?? c.notFound()
})

if (isMainModule(import.meta.url)) {
	const port = Number(process.env['PORT'] || 4000)

	serve({ fetch: app.fetch, port }, info => {
		console.log(`Hono SSR server listening on http://localhost:${info.port}`)
	})
}

export const reqHandler = createRequestHandler(app.fetch)
