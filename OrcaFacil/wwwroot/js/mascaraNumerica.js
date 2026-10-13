window.OrcaFacilMascara = {
    ligar: function (raiz, dotnet) {
        const input = raiz && raiz.querySelector("input, textarea");
        if (!input || input._ofMascara)
            return !!input;

        let fila = Promise.resolve();
        let suprimeInput = 0;
        let tratadoNaTecla = false;

        function cursorNoFim() {
            const fim = input.value.length;
            try {
                input.setSelectionRange(fim, fim);
            } catch {
                /* alguns tipos de teclado não aceitam seleção */
            }
        }

        function agendar(producao) {
            fila = fila.then(async function () {
                const novo = await producao();
                const texto = novo ?? "";
                if (input.value !== texto) {
                    suprimeInput++;
                    input.value = texto;
                }
                if (document.activeElement === input)
                    cursorNoFim();
            }).catch(function () { });
        }

        function selecaoTotal() {
            return input.selectionStart === 0
                && input.selectionEnd === input.value.length
                && input.value.length > 0;
        }

        function apagarOuLimpar() {
            agendar(function () {
                return dotnet.invokeMethodAsync(selecaoTotal() ? "Limpar" : "Apagar");
            });
        }

        function incluirOuSubstituir(texto) {
            const substitui = input.selectionStart !== input.selectionEnd;
            agendar(function () {
                return dotnet.invokeMethodAsync(substitui ? "DefinirTexto" : "IncluirTexto", texto ?? "");
            });
        }

        function aoKeyDown(e) {
            if (e.ctrlKey || e.metaKey || e.altKey)
                return;

            if (e.key === "ArrowLeft" || e.key === "ArrowRight" || e.key === "Home" || e.key === "End") {
                e.preventDefault();
                cursorNoFim();
                return;
            }

            const digito = e.key >= "0" && e.key <= "9";
            const apagar = e.key === "Backspace" || e.key === "Delete";
            if (!digito && !apagar)
                return;

            e.preventDefault();
            tratadoNaTecla = true;
            queueMicrotask(function () { tratadoNaTecla = false; });

            if (digito)
                incluirOuSubstituir(e.key);
            else
                apagarOuLimpar();
        }

        function aoBeforeInput(e) {
            const tipo = e.inputType || "";
            if (tratadoNaTecla) {
                e.preventDefault();
                return;
            }

            if (tipo.indexOf("delete") === 0) {
                e.preventDefault();
                apagarOuLimpar();
                return;
            }

            if (tipo === "insertFromPaste" || tipo === "insertFromDrop") {
                e.preventDefault();
                if (e.data)
                    agendar(function () { return dotnet.invokeMethodAsync("DefinirTexto", e.data); });
                return;
            }

            if (tipo === "insertText" || tipo === "insertReplacementText") {
                e.preventDefault();
                incluirOuSubstituir(e.data || "");
            }
        }

        function aoPaste(e) {
            e.preventDefault();
            const texto = e.clipboardData ? e.clipboardData.getData("text") : "";
            agendar(function () { return dotnet.invokeMethodAsync("DefinirTexto", texto); });
        }

        function aoDrop(e) {
            e.preventDefault();
            const texto = e.dataTransfer ? e.dataTransfer.getData("text") : "";
            agendar(function () { return dotnet.invokeMethodAsync("DefinirTexto", texto); });
        }

        function aoInput() {
            if (suprimeInput > 0) {
                suprimeInput--;
                return;
            }

            const atual = input.value;
            agendar(function () { return dotnet.invokeMethodAsync("DefinirTexto", atual); });
        }

        function aoPosicionar() {
            cursorNoFim();
        }

        input.addEventListener("keydown", aoKeyDown);
        input.addEventListener("beforeinput", aoBeforeInput);
        input.addEventListener("paste", aoPaste);
        input.addEventListener("drop", aoDrop);
        input.addEventListener("input", aoInput);
        input.addEventListener("focus", aoPosicionar);
        input.addEventListener("pointerup", aoPosicionar);

        input._ofMascara = {
            aoKeyDown: aoKeyDown,
            aoBeforeInput: aoBeforeInput,
            aoPaste: aoPaste,
            aoDrop: aoDrop,
            aoInput: aoInput,
            aoPosicionar: aoPosicionar
        };
        return true;
    },

    desligar: function (raiz) {
        const input = raiz && raiz.querySelector("input, textarea");
        if (!input || !input._ofMascara)
            return;

        const eventos = input._ofMascara;
        input.removeEventListener("keydown", eventos.aoKeyDown);
        input.removeEventListener("beforeinput", eventos.aoBeforeInput);
        input.removeEventListener("paste", eventos.aoPaste);
        input.removeEventListener("drop", eventos.aoDrop);
        input.removeEventListener("input", eventos.aoInput);
        input.removeEventListener("focus", eventos.aoPosicionar);
        input.removeEventListener("pointerup", eventos.aoPosicionar);
        delete input._ofMascara;
    },

    cursorNoFim: function (raiz) {
        const input = raiz && raiz.querySelector("input, textarea");
        if (!input || document.activeElement !== input)
            return;

        const fim = input.value.length;
        try {
            input.setSelectionRange(fim, fim);
        } catch {
            /* ignora quando o campo não aceita seleção */
        }
    }
};
