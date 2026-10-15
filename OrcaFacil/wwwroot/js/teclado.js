window.OrcaFacilTeclado = {
    iniciar: function () {
        const raiz = document.documentElement;
        const vv = window.visualViewport;
        if (!vv || raiz.dataset.ofTeclado)
            return;

        raiz.dataset.ofTeclado = "1";

        function aplicar() {
            const coberto = Math.round(window.innerHeight - vv.height - vv.offsetTop);
            const teclado = coberto > 80 ? coberto : 0;
            raiz.style.setProperty("--of-teclado", teclado + "px");
            raiz.classList.toggle("of-teclado-aberto", teclado > 0);
            if (teclado > 0)
                requestAnimationFrame(rolarCampo);
        }

        function rolarCampo() {
            const el = document.activeElement;
            if (!el || (el.tagName !== "INPUT" && el.tagName !== "TEXTAREA"))
                return;

            const alvo = el.closest(".mud-input-control") || el;
            const caixa = alvo.getBoundingClientRect();
            const limite = vv.offsetTop + vv.height - 16;
            if (caixa.bottom <= limite && caixa.top >= vv.offsetTop + 8)
                return;

            const delta = caixa.bottom - (limite - 12);
            const scroller = scrollPai(alvo);
            if (scroller)
                scroller.scrollTop += delta;
            else
                window.scrollBy(0, delta);
        }

        function scrollPai(el) {
            let pai = el.parentElement;
            while (pai) {
                const estilo = window.getComputedStyle(pai);
                const rola = estilo.overflowY === "auto" || estilo.overflowY === "scroll";
                if (rola && pai.scrollHeight > pai.clientHeight + 1)
                    return pai;
                pai = pai.parentElement;
            }
            return null;
        }

        vv.addEventListener("resize", aplicar);
        document.addEventListener("focusin", function () {
            setTimeout(aplicar, 60);
            setTimeout(aplicar, 320);
        });
    }
};

window.OrcaFacilTeclado.iniciar();
