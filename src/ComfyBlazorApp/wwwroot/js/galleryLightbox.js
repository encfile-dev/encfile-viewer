import PhotoSwipeLightbox from '/lib/photoswipe/dist/photoswipe-lightbox.esm.js';

const instances = new Map();
const openState = new Map();

export function syncGallery(galleryId) {
    disposeGallery(galleryId);

    const gallery = document.getElementById(galleryId);
    if (!gallery) {
        return;
    }

    const lightbox = new PhotoSwipeLightbox({
        gallery: `#${galleryId}`,
        children: 'a[data-pswp-index]',
        pswpModule: () => import('/lib/photoswipe/dist/photoswipe.esm.js'),
        bgOpacity: 0.92,
        showHideAnimationType: 'zoom',
        wheelToZoom: true
    });

    lightbox.addFilter('itemData', (itemData) => {
        const element = itemData.element;
        if (!element) {
            return itemData;
        }

        const image = element.querySelector('img');
        const width = image?.naturalWidth || Number.parseInt(element.dataset.pswpWidth || '1600', 10);
        const height = image?.naturalHeight || Number.parseInt(element.dataset.pswpHeight || '1200', 10);

        return {
            ...itemData,
            src: element.href,
            msrc: image?.currentSrc || element.href,
            width,
            height,
            alt: image?.alt || ''
        };
    });

    lightbox.on('open', () => {
        openState.set(galleryId, true);
    });

    lightbox.on('close', () => {
        openState.set(galleryId, false);
    });

    lightbox.init();
    instances.set(galleryId, lightbox);
    openState.set(galleryId, false);
}

export function isGalleryOpen(galleryId) {
    return openState.get(galleryId) === true;
}

export function disposeGallery(galleryId) {
    const existing = instances.get(galleryId);
    if (!existing) {
        return;
    }

    existing.destroy();
    instances.delete(galleryId);
    openState.delete(galleryId);
}
