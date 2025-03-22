using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZXing;

namespace Etiqueta
{
    // Enumeración para definir las opciones de alineación
    public enum AlineacionHorizontal
    {
        Izquierda,
        Centro,
        Derecha
    }

    public interface ICondicion
    {
        bool Evaluar(object contexto);
    }

    public class LambdaCondicion : ICondicion
    {
        private readonly Func<object, bool> _condicion;

        public LambdaCondicion(Func<object, bool> condicion)
        {
            _condicion = condicion;
        }

        public bool Evaluar(object contexto)
        {
            return _condicion(contexto);
        }
    }

    public abstract class ElementoEtiqueta
    {
        public float X { get; set; }
        public float Y { get; set; }

        public ElementoEtiqueta(float x, float y)
        {
            X = x;
            Y = y;
        }

        public abstract void Dibujar(Graphics graphics, object contexto);
        public abstract void Escalar(float factor);
        public abstract float ObtenerAltura();
        public abstract float ObtenerAncho(Graphics graphics); // Nuevo método para calcular el ancho
    }

    public class ElementoTexto : ElementoEtiqueta
    {
        public string Texto { get; set; }
        public Font Fuente { get; set; }
        public Brush Color { get; set; }

        public ElementoTexto(string texto, float x, float y, Font fuente, Brush color)
            : base(x, y)
        {
            Texto = texto;
            Fuente = fuente;
            Color = color;
        }

        public override void Dibujar(Graphics graphics, object contexto)
        {
            graphics.DrawString(Texto, Fuente, Color, X, Y);
        }

        public override void Escalar(float factor)
        {
            X *= factor;
            Y *= factor;
            Fuente = new Font(Fuente.FontFamily, Fuente.Size * factor, Fuente.Style);
        }

        public override float ObtenerAltura()
        {
            return Fuente.GetHeight();
        }

        public override float ObtenerAncho(Graphics graphics)
        {
            return graphics.MeasureString(Texto, Fuente).Width;
        }
    }

    public class ElementoCodigoBarras : ElementoEtiqueta
    {
        public string Codigo { get; set; }
        public Rectangle Rectangulo { get; set; }

        public ElementoCodigoBarras(string codigo, float x, float y, int ancho, int alto)
            : base(x, y)
        {
            Codigo = codigo;
            Rectangulo = new Rectangle((int)x, (int)y, ancho, alto);
        }

        public override void Dibujar(Graphics graphics, object contexto)
        {
            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.CODE_128,
                Options = new ZXing.Common.EncodingOptions
                {
                    Width = Rectangulo.Width,
                    Height = Rectangulo.Height,
                    Margin = 0
                }
            };
            var bitmap = writer.Write(Codigo);
            graphics.DrawImage(bitmap, Rectangulo);
        }

        public override void Escalar(float factor)
        {
            X *= factor;
            Y *= factor;
            Rectangulo = new Rectangle(
                (int)(Rectangulo.X * factor),
                (int)(Rectangulo.Y * factor),
                (int)(Rectangulo.Width * factor),
                (int)(Rectangulo.Height * factor));
        }

        public override float ObtenerAltura()
        {
            return Rectangulo.Height;
        }

        public override float ObtenerAncho(Graphics graphics)
        {
            return Rectangulo.Width;
        }
    }

    public class ElementoCondicional : ElementoEtiqueta
    {
        public ElementoEtiqueta Elemento { get; }
        public ICondicion Condicion { get; }

        public ElementoCondicional(ElementoEtiqueta elemento, ICondicion condicion)
            : base(elemento.X, elemento.Y)
        {
            Elemento = elemento;
            Condicion = condicion;
        }

        public override void Dibujar(Graphics graphics, object contexto)
        {
            if (Condicion.Evaluar(contexto))
            {
                Elemento.Dibujar(graphics, contexto);
            }
        }

        public override void Escalar(float factor)
        {
            X *= factor;
            Y *= factor;
            Elemento.Escalar(factor);
        }

        public override float ObtenerAltura()
        {
            return Elemento.ObtenerAltura();
        }

        public override float ObtenerAncho(Graphics graphics)
        {
            return Elemento.ObtenerAncho(graphics);
        }
    }

    public class Etiqueta
    {
        internal readonly List<ElementoEtiqueta> _elementos = new List<ElementoEtiqueta>();
        public float Ancho { get; private set; }
        public float Alto { get; private set; }

        public Etiqueta(float ancho, float alto)
        {
            Ancho = ancho;
            Alto = alto;
        }

        public void AgregarElemento(ElementoEtiqueta elemento)
        {
            _elementos.Add(elemento);
        }

        public void Escalar(float factor)
        {
            Ancho *= factor;
            Alto *= factor;
            foreach (var elemento in _elementos)
            {
                elemento.Escalar(factor);
            }
        }

        public void Generar(Action<Bitmap> renderAction, object contexto = null)
        {
            using (var bitmap = new Bitmap((int)Ancho, (int)Alto))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

                    foreach (var elemento in _elementos)
                    {
                        elemento.Dibujar(graphics, contexto);
                    }
                }

                renderAction(bitmap);
            }
        }
    }

    public class EtiquetaBuilder
    {
        private readonly Etiqueta _etiqueta;
        private object _contexto;
        private float _ultimaY;
        private bool _condicionEjecutada;
        public EtiquetaBuilder(float ancho, float alto)
        {
            _etiqueta = new Etiqueta(ancho, alto);
            _ultimaY = 0;
        }

        public EtiquetaBuilder ConContexto(object contexto)
        {
            _contexto = contexto;
            return this;
        }

        public EtiquetaBuilder AgregarTexto(string texto, float x, float y, Font fuente, Brush color = null, AlineacionHorizontal alineacion = AlineacionHorizontal.Izquierda)
        {
            var elemento = new ElementoTexto(texto, x, y, fuente, color ?? Brushes.Black);
            AlinearElemento(elemento, alineacion);
            _etiqueta.AgregarElemento(elemento);
            _ultimaY = Math.Max(_ultimaY, y + elemento.ObtenerAltura());
            return this;
        }

        public EtiquetaBuilder AgregarCodigoBarras(string codigo, float x, float y, int ancho, int alto, AlineacionHorizontal alineacion = AlineacionHorizontal.Izquierda)
        {
            var elemento = new ElementoCodigoBarras(codigo, x, y, ancho, alto);
            AlinearElemento(elemento, alineacion);
            _etiqueta.AgregarElemento(elemento);
            _ultimaY = Math.Max(_ultimaY, y + elemento.ObtenerAltura());
            return this;
        }

        public EtiquetaBuilder AgregarTextoDividido(string texto, float x, float y, Font fuente, int longitudMaxima, float espaciadoVertical, Brush color = null, AlineacionHorizontal alineacion = AlineacionHorizontal.Izquierda)
        {
            if (string.IsNullOrEmpty(texto))
            {
                texto = "";
            }

            if (fuente == null)
            {
                throw new ArgumentNullException(nameof(fuente), "La fuente no puede ser null.");
            }

            var lineas = DividirTexto(texto, longitudMaxima);
            for (int i = 0; i < lineas.Count; i++)
            {
                float yPos = y + (i * espaciadoVertical);
                if (yPos < 0) yPos = 0;

                var elemento = new ElementoTexto(lineas[i], x, yPos, fuente, color ?? Brushes.Black);
                AlinearElemento(elemento, alineacion);

                using (var bitmap = new Bitmap(1, 1))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    float anchoElemento = elemento.ObtenerAncho(graphics);
                    if (elemento.X < 0) elemento.X = 0;
                    if (elemento.X + anchoElemento > _etiqueta.Ancho) elemento.X = _etiqueta.Ancho - anchoElemento;
                }

                _etiqueta.AgregarElemento(elemento);
                _ultimaY = Math.Max(_ultimaY, elemento.Y + elemento.ObtenerAltura());

#if DEBUG
                Console.WriteLine($"Texto: '{lineas[i]}', X: {elemento.X}, Y: {elemento.Y}, Altura: {elemento.ObtenerAltura()}");
#endif
            }
            return this;
        }

        private List<string> DividirTexto(string texto, int longitudMaxima)
        {
            var lineas = new List<string>();
            if (string.IsNullOrEmpty(texto))
            {
                lineas.Add("");
                return lineas;
            }

            while (texto.Length > longitudMaxima)
            {
                lineas.Add(texto.Substring(0, longitudMaxima));
                texto = texto.Substring(longitudMaxima);
            }
            if (!string.IsNullOrEmpty(texto))
            {
                lineas.Add(texto);
            }
            return lineas;
        }

        private void AlinearElemento(ElementoEtiqueta elemento, AlineacionHorizontal alineacion)
        {
            using (var bitmap = new Bitmap(1, 1))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                float anchoElemento = elemento.ObtenerAncho(graphics);
                switch (alineacion)
                {
                    case AlineacionHorizontal.Izquierda:
                        elemento.X = 0 + 5;
                        break;
                    case AlineacionHorizontal.Centro:
                        elemento.X = (_etiqueta.Ancho - anchoElemento) / 2f;
                        break;
                    case AlineacionHorizontal.Derecha:
                        elemento.X = _etiqueta.Ancho - anchoElemento - 5;
                        break;
                }
            }
        }
        public EtiquetaBuilder If(Func<object, bool> condicion, Action<EtiquetaBuilder> configuracion)
        {
            _condicionEjecutada = false;
            if (!_condicionEjecutada && condicion(_contexto))
            {
                configuracion(this);
                _condicionEjecutada = true;
            }
            return this;
        }

        public EtiquetaBuilder ElseIf(Func<object, bool> condicion, Action<EtiquetaBuilder> configuracion)
        {
            if (!_condicionEjecutada && condicion(_contexto))
            {
                configuracion(this);
                _condicionEjecutada = true;
            }
            return this;
        }

        public EtiquetaBuilder Else(Action<EtiquetaBuilder> configuracion)
        {
            if (!_condicionEjecutada)
            {
                configuracion(this);
                _condicionEjecutada = true;
            }
            return this;
        }

        // Método For
        public EtiquetaBuilder For(int inicio, int fin, Action<EtiquetaBuilder, int> configuracion)
        {
            for (int i = inicio; i < fin; i++)
            {
                configuracion(this, i);
            }
            return this;
        }

        public EtiquetaBuilder ForEach<T>(IEnumerable<T> items, Action<EtiquetaBuilder, T> configuracion)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items), "La colección no puede ser null.");

            foreach (var item in items)
            {
                configuracion(this, item);
            }
            return this;
        }
        public EtiquetaBuilder Escalar(float factor)
        {
            if (factor <= 0)
                throw new ArgumentException("El factor de escala debe ser mayor que 0.");
            _etiqueta.Escalar(factor);
            _ultimaY *= factor;
            return this;
        }

        public EtiquetaBuilder EscalarDinamicamente(float anchoObjetivo, float altoObjetivo)
        {
            if (anchoObjetivo <= 0 || altoObjetivo <= 0)
                throw new ArgumentException("Las dimensiones objetivo deben ser mayores que 0.");

            float factorAncho = anchoObjetivo / _etiqueta.Ancho;
            float factorAlto = altoObjetivo / _etiqueta.Alto;
            float factor = Math.Min(factorAncho, factorAlto);

            _etiqueta.Escalar(factor);
            _ultimaY *= factor;
            return this;
        }

        public float ObtenerUltimaY()
        {
            return _ultimaY;
        }

        public EtiquetaBuilder CentrarVerticalmente()
        {
            if (_etiqueta._elementos.Count == 0)
                return this;

            float alturaTotal = _ultimaY;
            float desplazamiento = (_etiqueta.Alto - alturaTotal) / 2f;

            foreach (var elemento in _etiqueta._elementos)
            {
                elemento.Y += desplazamiento;
            }

            _ultimaY += desplazamiento;
            return this;
        }

        public Etiqueta Construir()
        {
            return _etiqueta;
        }

        public EtiquetaBuilder Generar(Action<Bitmap> renderAction)
        {
            _etiqueta.Generar(renderAction, _contexto);
            _condicionEjecutada = false;
            return this;
        }

        public EtiquetaBuilder Mostrar()
        {
            _etiqueta.Generar(bitmap =>
            {
                PictureBox pictureBox = new PictureBox
                {
                    Size = new Size((int)bitmap.Width, (int)bitmap.Height),
                    Image = (Bitmap)bitmap.Clone()
                };
                Form form = new Form
                {
                    Size = new Size((int)bitmap.Width + 20, (int)bitmap.Height + 40),
                    StartPosition = FormStartPosition.CenterScreen
                };
                form.Controls.Add(pictureBox);
                form.ShowDialog();
            }, _contexto);
            return this;
        }
    }
}