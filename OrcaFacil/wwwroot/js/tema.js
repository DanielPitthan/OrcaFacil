window.OrcaFacilTema = {
    aplicar: function (classe) {
        const nomes = ["of-tema-claro", "of-tema-escuro"];
        const alvos = [document.documentElement, document.body];
        for (let i = 0; i < alvos.length; i++) {
            const el = alvos[i];
            if (!el)
                continue;
            for (let j = 0; j < nomes.length; j++)
                el.classList.remove(nomes[j]);
            if (classe)
                el.classList.add(classe);
        }
    }
};
