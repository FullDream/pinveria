import Map from 'ol/Map.js'
import View from 'ol/View.js'
import Projection from 'ol/proj/Projection.js'
import { addCoordinateTransforms } from 'ol/proj.js'
import { remove as removeTransform } from 'ol/proj/transforms.js'
import { unByKey } from 'ol/Observable.js'
import TileGrid from 'ol/tilegrid/TileGrid.js'
import TileLayer from 'ol/layer/Tile.js'
import XYZ from 'ol/source/XYZ.js'
import { defaults as defaultInteractions } from 'ol/interaction/defaults.js'
import { MapMarkers } from './map-markers'
import {
	LOGICAL_EXTENT,
	LOGICAL_SPAN,
	type MapViewStyle,
	type MapMarker,
	type MapTool,
	type MarkerInteractions,
	type VisibleMapRegion,
} from './map-viewport-types'

export class MapEngine {
	readonly #markers: MapMarkers
	readonly #layer = new TileLayer<XYZ>()
	readonly #map: Map
	readonly #observer: ResizeObserver

	#fitExtent: [number, number, number, number] = [...LOGICAL_EXTENT]
	#url: string | null = null
	#fitted = false
	#sourceProjection: Projection | undefined

	constructor(target: HTMLElement, mapId: string, interactions: MarkerInteractions) {
		const projection = new Projection({
			code: `game-map:${mapId}:${crypto.randomUUID()}`,
			units: 'pixels',
			extent: [...LOGICAL_EXTENT],
			metersPerUnit: 1,
			getPointResolution: resolution => resolution,
		})

		const view = new View({
			projection,
			center: [0, 0],
			maxResolution: LOGICAL_SPAN / 256,
			minResolution: LOGICAL_SPAN / (16384 * 4),
			zoom: 0,
			enableRotation: false,
			constrainResolution: false,
		})

		this.#layer.setExtent([...LOGICAL_EXTENT])

		this.#map = new Map({
			target,
			layers: [this.#layer],
			view,
			controls: [],
			interactions: defaultInteractions({ onFocusOnly: false }),
		})

		this.#markers = new MapMarkers(this.#map, interactions)

		this.#observer = new ResizeObserver(() => {
			this.#map.updateSize()
			interactions.viewportChanged?.()

			if (!this.#fitted && target.clientWidth && target.clientHeight) {
				this.fit()
				this.#fitted = true
			}
		})

		this.#observer.observe(target)
	}

	setStyle(style: MapViewStyle | undefined): void {
		const url = style?.tilesUrlTemplate ?? null

		if (url === this.#url) return

		this.#url = url

		const geometry = style?.tileGeometry
		const previousSource = this.#layer.getSource()
		const targetProjection = this.#map.getView().getProjection()

		this.#layer.setSource(null)
		previousSource?.dispose()
		this.#releaseTransforms()
		this.#fitExtent = [...LOGICAL_EXTENT]
		this.#layer.setExtent([...LOGICAL_EXTENT])

		if (!url || !geometry || !style) return

		const srcWidth = geometry.extent[2] - geometry.extent[0]
		const srcHeight = geometry.extent[3] - geometry.extent[1]
		const scale = LOGICAL_SPAN / Math.max(srcWidth, srcHeight)
		const halfWidth = (srcWidth * scale) / 2
		const halfHeight = (srcHeight * scale) / 2
		const styleExtent: [number, number, number, number] = [-halfWidth, -halfHeight, halfWidth, halfHeight]

		this.#fitExtent = styleExtent
		this.#layer.setExtent(styleExtent)

		const sourceProjection = new Projection({
			code: `${targetProjection.getCode()}:style:${style.id}`,
			units: 'pixels',
			extent: geometry.extent,
			metersPerUnit: scale,
			getPointResolution: resolution => resolution,
		})

		addCoordinateTransforms(
			sourceProjection,
			targetProjection,
			coordinate => [coordinate[0] * scale, coordinate[1] * scale],
			coordinate => [coordinate[0] / scale, coordinate[1] / scale],
		)

		this.#sourceProjection = sourceProjection

		this.#layer.setSource(
			new XYZ({
				url,
				projection: sourceProjection,
				wrapX: false,
				maxZoom: geometry.maxZoom,
				tileGrid: new TileGrid({
					extent: geometry.extent,
					origin: geometry.tileOrigin,
					tileSize: geometry.tileSize,
					resolutions: geometry.resolutions,
					minZoom: geometry.minZoom,
				}),
			}),
		)
	}

	zoom(delta: number): void {
		this.#map.getView().adjustZoom(delta)
	}

	setMarkers(markers: MapMarker[], tool: MapTool): void {
		this.#markers.setMarkers(markers)
		this.#markers.setTool(tool)
	}

	reveal(id: string, region: VisibleMapRegion, duration = 0): void {
		this.#markers.reveal(id, region, duration)
	}

	fit(): void {
		this.#map.getView().fit(this.#fitExtent, { size: this.#map.getSize(), padding: [24, 24, 24, 24] })
	}

	getPixel(coordinate: [number, number]): [number, number] | null {
		const pixel = this.#map.getPixelFromCoordinate(coordinate)

		if (!pixel || pixel.length < 2) return null

		return [pixel[0], pixel[1]]
	}

	onPostRender(callback: () => void): () => void {
		const key = this.#map.on('postrender', callback)

		return () => unByKey(key)
	}

	dispose(): void {
		this.#markers.dispose()
		this.#observer.disconnect()
		this.#map.setTarget(undefined)
		this.#layer.getSource()?.dispose()
		this.#releaseTransforms()
		this.#layer.dispose()
		this.#map.dispose()
	}

	#releaseTransforms(): void {
		if (!this.#sourceProjection) return

		const target = this.#map.getView().getProjection()

		removeTransform(this.#sourceProjection, target)
		removeTransform(target, this.#sourceProjection)
		this.#sourceProjection = undefined
	}
}
