(function () {
  const header = document.querySelector('[data-sticky-header]');
  if (!header) return;

  const stickyThreshold = 24;
  const onScroll = () => {
    if (window.scrollY > stickyThreshold) {
      header.classList.add('header-sticky');
    } else {
      header.classList.remove('header-sticky');
    }
  };

  onScroll();
  window.addEventListener('scroll', onScroll, { passive: true });
})();
