# Etiqueta

Este proyecto proporciona una biblioteca para generar etiquetas con texto y códigos de barras en C# utilizando `System.Drawing` y `ZXing`.

## Características

- Agregar texto con alineación configurable.
- Agregar códigos de barras en formato `CODE_128`.
- Evaluar condiciones para mostrar elementos condicionalmente.
- Soporte para escalar etiquetas dinámicamente.
- Construcción flexible de etiquetas con `EtiquetaBuilder`.

## Instalación

Para utilizar esta biblioteca, asegúrate de agregar `ZXing.Net` como dependencia en tu proyecto:

```sh
Install-Package ZXing.Net
```

## Uso

### Crear una etiqueta con texto y código de barras

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using Etiqueta;

class Program
{
    static void Main()
    {
        var etiqueta = new EtiquetaBuilder(300, 150)
            .AgregarTexto("Producto: Zapatos", 10, 10, new Font("Arial", 12), Brushes.Black)
            .AgregarCodigoBarras("123456789", 10, 40, 200, 50)
            .Construir();

        etiqueta.Generar(bitmap => bitmap.Save("etiqueta.png"));
    }
}
```

### Construcción condicional de etiquetas

```csharp
var builder = new EtiquetaBuilder(200, 100)
    .ConContexto(new { Cantidad = 3 });

Font font = new Font("Verdana", 8);
var items = new List<string> { "Item 1", "Item 2", "Item 3" };

builder
    .If(ctx => ((dynamic)ctx).Cantidad > 2, b =>
    {
        b.AgregarTexto("Muchos ítems", 0, 0, font, Brushes.Black, AlineacionHorizontal.Centro);
    })
    .Else(b =>
    {
        b.AgregarTexto("Pocos ítems", 0, 0, font, Brushes.Black, AlineacionHorizontal.Centro);
    })
    .ForEach(items, (b, item) =>
    {
        b.AgregarTexto(item, 0, b.ObtenerUltimaY() + 5, font, Brushes.Blue, AlineacionHorizontal.Izquierda);
    })
    .CentrarVerticalmente()
    .Generar(bitmap => bitmap.Save("etiqueta_control.png", System.Drawing.Imaging.ImageFormat.Png))
    .Mostrar();
```

### Escalar dinámicamente

```csharp
var etiqueta = new EtiquetaBuilder(300, 150)
    .AgregarTexto("Ejemplo", 10, 10, new Font("Arial", 12), Brushes.Black)
    .EscalarDinamicamente(600, 300)
    .Construir();
```

### Vista previa de la etiqueta en una ventana

```csharp
var etiqueta = new EtiquetaBuilder(300, 150)
    .AgregarTexto("Vista previa", 10, 10, new Font("Arial", 12), Brushes.Black)
    .Mostrar();
```

## Licencia

Este proyecto está bajo la licencia MIT.
