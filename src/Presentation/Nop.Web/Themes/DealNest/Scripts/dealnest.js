(function () {
    function ready(callback) {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", callback, { once: true });
            return;
        }

        callback();
    }

    ready(function () {
        var page = document.querySelector(".master-wrapper-page");
        var filterSidebar = document.getElementById("catalog-sidebar");
        var filterToggle = document.querySelector("[data-dealnest-filter-toggle]");
        var filterClose = document.querySelector("[data-dealnest-filter-close]");
        var menuContainers = Array.prototype.slice.call(document.querySelectorAll(".menu-container"));
        var desktopMedia = window.matchMedia("(min-width: 761px)");

        var syncScrollState = function () {
            document.body.classList.toggle("is-scrolled", window.scrollY > 12);
        };

        var normalizeTermsOfService = function (scope) {
            var root = scope || document;

            root.querySelectorAll(".terms-of-service").forEach(function (container) {
                if (container.hasAttribute("data-dealnest-normalized")) {
                    return;
                }

                var checkbox = container.querySelector('input[type="checkbox"]');
                var label = container.querySelector("label");
                var read = container.querySelector(".read");

                if (!checkbox || !label) {
                    return;
                }

                var copy = document.createElement("span");
                copy.className = "terms-of-service__copy";

                container.insertBefore(copy, read || null);
                copy.appendChild(label);

                if (read) {
                    copy.appendChild(document.createTextNode(" "));
                    copy.appendChild(read);
                }

                container.setAttribute("data-dealnest-normalized", "true");
            });
        };

        var normalizeRemoveButtons = function (scope) {
            var root = scope || document;

            root.querySelectorAll(".remove-from-cart").forEach(function (container) {
                var checkbox = container.querySelector('input[type="checkbox"][aria-label]');
                var button = container.querySelector(".remove-btn");
                var label = checkbox ? checkbox.getAttribute("aria-label") : "";

                if (!button || !label) {
                    return;
                }

                button.setAttribute("data-remove-label", label);
                button.setAttribute("aria-label", label);
            });
        };

        var normalizeRfqRequestList = function (scope) {
            var root = scope || document;

            root.querySelectorAll(".request-list-page .section").forEach(function (section) {
                var header = section.querySelector(".request-header");
                var info = section.querySelector(".info");
                var button = header ? header.querySelector(".button-2") : null;
                var buttonRow;

                if (!header || !info || !button) {
                    return;
                }

                buttonRow = section.querySelector(".request-list__buttons");

                if (!buttonRow) {
                    buttonRow = document.createElement("div");
                    buttonRow.className = "buttons request-list__buttons";
                }

                buttonRow.appendChild(button);
                section.appendChild(buttonRow);
            });
        };

        syncScrollState();
        window.addEventListener("scroll", syncScrollState, { passive: true });
        normalizeTermsOfService(document);
        normalizeRemoveButtons(document);
        normalizeRfqRequestList(document);

        if (typeof window.displayPopupContentFromUrl === "function" && !window.displayPopupContentFromUrl.__dealnestWrapped) {
            var originalDisplayPopupContentFromUrl = window.displayPopupContentFromUrl;

            window.displayPopupContentFromUrl = function (url, title, modal, width) {
                var normalizedUrl = typeof url === "string" ? url.toLowerCase() : "";
                var isTermsPopup = normalizedUrl.indexOf("conditionsofuse") !== -1;

                originalDisplayPopupContentFromUrl(url, title, isTermsPopup ? true : modal, width);

                if (isTermsPopup && window.jQuery) {
                    window.setTimeout(function () {
                        var $ = window.jQuery;

                        $(".ui-widget-overlay").off("click.dealnestDialog").on("click.dealnestDialog", function () {
                            var openDialog = $(".ui-dialog-content").filter(function () {
                                var instance = $(this).dialog("instance");
                                return !!instance && $(this).closest(".ui-dialog").is(":visible");
                            }).last();

                            if (openDialog.length) {
                                openDialog.dialog("close");
                            }
                        });
                    }, 0);
                }
            };

            window.displayPopupContentFromUrl.__dealnestWrapped = true;
        }

        var closeFilters = function () {
            if (!page || !filterSidebar || !filterToggle) {
                return;
            }

            page.classList.remove("filters-open");
            filterToggle.setAttribute("aria-expanded", "false");
        };

        if (filterToggle && filterSidebar && page) {
            filterToggle.addEventListener("click", function () {
                var isOpen = page.classList.toggle("filters-open");
                filterToggle.setAttribute("aria-expanded", isOpen ? "true" : "false");
            });
        }

        if (filterClose) {
            filterClose.addEventListener("click", closeFilters);
        }

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                closeFilters();
            }
        });

        document.addEventListener("click", function (event) {
            if (page && filterSidebar && filterToggle && page.classList.contains("filters-open")) {
                var clickedToggle = filterToggle.contains(event.target);
                var clickedSidebar = filterSidebar.contains(event.target);

                if (!clickedToggle && !clickedSidebar) {
                    closeFilters();
                }
            }

            var popupClose = event.target.closest("[data-dealnest-popup-close]");

            if (!popupClose) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            if (typeof event.stopImmediatePropagation === "function") {
                event.stopImmediatePropagation();
            }

            if (window.jQuery && window.jQuery.magnificPopup) {
                window.jQuery.magnificPopup.close();
            }
        });

        if (window.MutationObserver) {
            var observer = new MutationObserver(function (mutations) {
                mutations.forEach(function (mutation) {
                    mutation.addedNodes.forEach(function (node) {
                        if (node.nodeType !== 1) {
                            return;
                        }

                        if (node.matches && node.matches(".terms-of-service")) {
                            normalizeTermsOfService(node.parentNode || document);
                            return;
                        }

                        if (node.querySelector && node.querySelector(".terms-of-service")) {
                            normalizeTermsOfService(node);
                        }

                        if (node.matches && node.matches(".remove-from-cart")) {
                            normalizeRemoveButtons(node.parentNode || document);
                            return;
                        }

                        if (node.querySelector && node.querySelector(".remove-from-cart")) {
                            normalizeRemoveButtons(node);
                        }

                        if (node.matches && node.matches(".request-list-page")) {
                            normalizeRfqRequestList(node);
                            return;
                        }

                        if (node.querySelector && node.querySelector(".request-list-page")) {
                            normalizeRfqRequestList(node);
                        }

                    });
                });
            });

            observer.observe(document.body, {
                childList: true,
                subtree: true
            });
        }

        window.addEventListener("resize", function () {
            if (window.innerWidth >= 1001) {
                closeFilters();
            }
        });

        document.querySelectorAll("[data-dealnest-tabs]").forEach(function (tabSet) {
            var buttons = Array.prototype.slice.call(tabSet.querySelectorAll("[data-dealnest-tab-button]"));
            var panels = Array.prototype.slice.call(tabSet.querySelectorAll(".product-tabs__panel"));

            buttons.forEach(function (button) {
                button.addEventListener("click", function () {
                    var panelId = button.getAttribute("data-dealnest-tab-button");

                    buttons.forEach(function (item) {
                        var active = item === button;
                        item.classList.toggle("is-active", active);
                        item.setAttribute("aria-selected", active ? "true" : "false");
                    });

                    panels.forEach(function (panel) {
                        var active = panel.id === panelId;
                        panel.classList.toggle("is-active", active);
                        panel.hidden = !active;
                    });
                });
            });
        });

        document.querySelectorAll("[data-quantity-target]").forEach(function (button) {
            button.addEventListener("click", function () {
                var targetId = button.getAttribute("data-quantity-target");
                var delta = parseInt(button.getAttribute("data-quantity-delta"), 10);
                var input = document.getElementById(targetId);

                if (!input) {
                    return;
                }

                var current = parseInt(input.value || "1", 10);
                var next = isNaN(current) ? 1 : current + delta;
                input.value = String(Math.max(1, next));
                input.dispatchEvent(new Event("input", { bubbles: true }));
                input.dispatchEvent(new Event("change", { bubbles: true }));
            });
        });

        menuContainers.forEach(function (container) {
            var toggle = container.querySelector(".menu__toggle");
            var topItems = Array.prototype.slice.call(container.querySelectorAll(".menu > .menu__item"));
            var subItems = Array.prototype.slice.call(container.querySelectorAll(".menu > .menu__item.menu-dropdown"));
            var closeTimer;
            var getSubmenuPanel = function (item) {
                return item ? item.querySelector(".menu__list-view, .menu__grid-view") : null;
            };

            var resetSubmenuPosition = function (item) {
                var panel = getSubmenuPanel(item);

                if (!panel) {
                    return;
                }

                panel.style.left = "";
                panel.style.right = "";
                panel.style.top = "";
                panel.style.width = "";
                panel.style.maxWidth = "";
                panel.style.maxHeight = "";
            };

            var positionSubmenu = function (item) {
                var panel = getSubmenuPanel(item);
                var viewportPadding = 16;
                var availableWidth;
                var availableHeight;
                var itemRect;
                var panelRect;
                var panelWidth;
                var panelHeight;
                var left;
                var desiredLeft;
                var top;
                var desiredTop;
                var remainingWidth;

                if (!panel) {
                    return;
                }

                panel.style.left = "";
                panel.style.right = "";
                panel.style.top = "";
                itemRect = item.getBoundingClientRect();
                availableHeight = Math.max(220, window.innerHeight - (viewportPadding * 2));
                panel.style.maxHeight = availableHeight + "px";

                if (window.innerWidth <= 760) {
                    remainingWidth = window.innerWidth - itemRect.right - viewportPadding - 8;
                    availableWidth = remainingWidth >= 112
                        ? remainingWidth
                        : Math.max(112, window.innerWidth - itemRect.left - viewportPadding - 8);
                    panel.style.width = availableWidth + "px";
                } else {
                    availableWidth = Math.max(260, window.innerWidth - (viewportPadding * 2));
                    panel.style.width = "";
                }
                panel.style.maxWidth = availableWidth + "px";

                panelRect = panel.getBoundingClientRect();
                panelWidth = Math.min(panelRect.width || panel.scrollWidth || availableWidth, availableWidth);
                panelHeight = Math.min(panelRect.height || panel.scrollHeight || availableHeight, availableHeight);
                desiredLeft = window.innerWidth <= 760 ? itemRect.right + 8 : itemRect.left;
                left = desiredLeft;

                if (left + panelWidth > window.innerWidth - viewportPadding) {
                    left = window.innerWidth - viewportPadding - panelWidth;
                }

                if (left < viewportPadding) {
                    left = viewportPadding;
                }

                panel.style.left = (left - itemRect.left) + "px";
                panel.style.right = "auto";

                if (window.innerWidth <= 760) {
                    desiredTop = itemRect.top;
                    top = desiredTop;

                    if (top + panelHeight > window.innerHeight - viewportPadding) {
                        top = window.innerHeight - viewportPadding - panelHeight;
                    }

                    if (top < viewportPadding) {
                        top = viewportPadding;
                    }

                    panel.style.top = (top - itemRect.top) + "px";
                }
            };

            var clearCloseTimer = function () {
                if (closeTimer) {
                    window.clearTimeout(closeTimer);
                    closeTimer = 0;
                }
            };

            var closeSubItems = function () {
                subItems.forEach(function (item) {
                    item.classList.remove("menu-dropdown--active");
                    var link = item.querySelector(".menu__link[aria-expanded]");
                    if (link) {
                        link.setAttribute("aria-expanded", "false");
                    }
                });
            };

            var openSubItem = function (item) {
                var link;

                if (!item) {
                    return;
                }

                clearCloseTimer();
                closeSubItems();
                item.classList.add("menu-dropdown--active");
                positionSubmenu(item);
                link = item.querySelector(".menu__link[aria-expanded]");
                if (link) {
                    link.setAttribute("aria-expanded", "true");
                }
            };

            var activateFirstSubItem = function () {
                var activeItem;

                if (!desktopMedia.matches || !subItems.length) {
                    return;
                }

                activeItem = subItems.find(function (item) {
                    return item.classList.contains("menu-dropdown--active");
                });

                if (!activeItem) {
                    openSubItem(subItems[0]);
                }
            };

            var closeContainer = function () {
                clearCloseTimer();
                container.classList.remove("menu-dropdown--active");
                if (toggle) {
                    toggle.setAttribute("aria-expanded", "false");
                }
                closeSubItems();
            };

            var scheduleCloseContainer = function () {
                clearCloseTimer();
                closeTimer = window.setTimeout(closeContainer, 240);
            };

            var openContainer = function () {
                clearCloseTimer();
                container.classList.add("menu-dropdown--active");
                if (toggle) {
                    toggle.setAttribute("aria-expanded", "true");
                }
                activateFirstSubItem();
            };

            if (toggle) {
                toggle.setAttribute("aria-expanded", container.classList.contains("menu-dropdown--active") ? "true" : "false");
            }

            if (toggle) {
                toggle.addEventListener("focusin", function () {
                    if (desktopMedia.matches) {
                        openContainer();
                    }
                });
                toggle.addEventListener("click", function () {
                    window.setTimeout(function () {
                        if (!desktopMedia.matches) {
                            return;
                        }

                        if (container.classList.contains("menu-dropdown--active")) {
                            activateFirstSubItem();
                            return;
                        }

                        closeSubItems();
                    }, 0);
                });
            }

            container.addEventListener("mouseleave", function () {
                if (desktopMedia.matches) {
                    scheduleCloseContainer();
                }
            });
            container.addEventListener("focusout", function () {
                if (!desktopMedia.matches) {
                    return;
                }

                window.setTimeout(function () {
                    if (!container.contains(document.activeElement)) {
                        scheduleCloseContainer();
                    }
                }, 0);
            });

            topItems.forEach(function (item) {
                if (subItems.indexOf(item) !== -1) {
                    return;
                }

                item.addEventListener("mouseenter", function () {
                    if (desktopMedia.matches) {
                        clearCloseTimer();
                        closeSubItems();
                    }
                });
                item.addEventListener("focusin", function () {
                    if (desktopMedia.matches) {
                        clearCloseTimer();
                        closeSubItems();
                    }
                });
            });

            subItems.forEach(function (item) {
                item.addEventListener("mouseenter", function () {
                    if (desktopMedia.matches) {
                        openContainer();
                        openSubItem(item);
                    }
                });
                item.addEventListener("focusin", function () {
                    if (desktopMedia.matches) {
                        openContainer();
                        openSubItem(item);
                    }
                });
                item.addEventListener("click", function () {
                    window.setTimeout(function () {
                        var link = item.querySelector(".menu__link[aria-expanded]");

                        if (desktopMedia.matches) {
                            openContainer();
                            openSubItem(item);
                            return;
                        }

                        subItems.forEach(function (otherItem) {
                            if (otherItem === item) {
                                return;
                            }

                            otherItem.classList.remove("menu-dropdown--active");
                            var otherLink = otherItem.querySelector(".menu__link[aria-expanded]");
                            if (otherLink) {
                                otherLink.setAttribute("aria-expanded", "false");
                            }
                        });

                        if (link) {
                            link.setAttribute("aria-expanded", item.classList.contains("menu-dropdown--active") ? "true" : "false");
                        }

                        if (item.classList.contains("menu-dropdown--active")) {
                            positionSubmenu(item);
                        } else {
                            resetSubmenuPosition(item);
                        }
                    }, 0);
                });
            });

            document.addEventListener("click", function (event) {
                if (!container.contains(event.target)) {
                    closeContainer();
                }
            });

            window.addEventListener("resize", function () {
                if (!desktopMedia.matches) {
                    closeSubItems();
                    subItems.forEach(resetSubmenuPosition);
                    return;
                }

                subItems.forEach(function (item) {
                    if (item.classList.contains("menu-dropdown--active")) {
                        positionSubmenu(item);
                    }
                });
            });
        });
    });
})();
