(() => {
    if (!document.body.classList.contains('student-area')) return;

    const pageBody = document.querySelector('.page-body');
    const root = pageBody?.firstElementChild;
    if (!root) return;

    const revealEntries = [];
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let blocks = [root];

    if (root.matches('.edurag-page, .chat-grid')) {
        root.classList.add('student-reveal-container');
        blocks = [...root.children];
    }

    blocks.forEach((block, blockIndex) => {
        let components = [];

        if (block.matches('.row')) {
            components = [...block.children].filter((child) => child.className.includes('col-'));
        }

        if (components.length > 0) {
            block.classList.add('student-reveal-container');
            components.forEach((component, componentIndex) => {
                revealEntries.push({
                    item: component,
                    delay: Math.min(componentIndex, 6) * 55
                });
            });
            return;
        }

        revealEntries.push({
            item: block,
            delay: Math.min(blockIndex, 4) * 55
        });
    });

    const revealItems = revealEntries.map((entry) => entry.item);
    revealEntries.forEach(({ item, delay }) => {
        item.classList.add('student-reveal-item');
        item.style.setProperty('--student-reveal-delay', `${delay}ms`);
    });

    if (reduceMotion || !('IntersectionObserver' in window)) {
        revealItems.forEach((item) => item.classList.add('is-student-visible'));
        return;
    }

    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) return;

            entry.target.classList.add('is-student-visible');
            observer.unobserve(entry.target);
        });
    }, {
        threshold: 0.08,
        rootMargin: '0px 0px -4% 0px'
    });

    revealItems.forEach((item) => observer.observe(item));
})();
