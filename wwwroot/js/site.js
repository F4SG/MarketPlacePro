document.addEventListener("DOMContentLoaded", () => {
    const rol = document.getElementById("Rol");
    const tienda = document.getElementById("NombreTienda");
    if (rol && tienda) {
        const actualizarTienda = () => {
            tienda.required = rol.value === "vendedor";
            tienda.setAttribute("aria-required", String(tienda.required));
        };
        rol.addEventListener("change", actualizarTienda);
        actualizarTienda();
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const metodo = document.getElementById("metodo-cobro");
    const referencia = document.getElementById("referencia-cobro");
    if (metodo && referencia) {
        const actualizar = () => { referencia.required = metodo.value !== "efectivo"; };
        metodo.addEventListener("change", actualizar); actualizar();
    }
    document.querySelectorAll("[data-foto]").forEach(input => {
        let anterior;
        input.addEventListener("change", () => {
            if (anterior) URL.revokeObjectURL(anterior);
            const imagen = input.parentElement.querySelector("img");
            const archivo = input.files[0];
            input.setCustomValidity(archivo && archivo.size > 3 * 1024 * 1024 ? "La imagen supera los 3 MB." : "");
            if (archivo && imagen && input.validity.valid) { anterior = URL.createObjectURL(archivo); imagen.src = anterior; }
        });
    });
});
