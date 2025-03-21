using System;
using System.Collections.Generic;
using System.Drawing;
using ZXing;

namespace Etiqueta
{
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

        public EtiquetaBuilder(float ancho, float alto)
        {
            _etiqueta = new Etiqueta(ancho, alto);
        }

        public EtiquetaBuilder ConContexto(object contexto)
        {
            _contexto = contexto;
            return this;
        }

        public EtiquetaBuilder AgregarTexto(string texto, float x, float y, Font fuente, Brush color = null)
        {
            _etiqueta.AgregarElemento(new ElementoTexto(texto, x, y, fuente, color ?? Brushes.Black));
            return this;
        }

        public EtiquetaBuilder AgregarCodigoBarras(string codigo, float x, float y, int ancho, int alto)
        {
            _etiqueta.AgregarElemento(new ElementoCodigoBarras(codigo, x, y, ancho, alto));
            return this;
        }

        public EtiquetaBuilder Si(Func<object, bool> condicion, Action<EtiquetaBuilder> configuracion)
        {
            var subBuilder = new EtiquetaBuilder(_etiqueta.Ancho, _etiqueta.Alto);
            configuracion(subBuilder);
            foreach (var elemento in subBuilder._etiqueta._elementos)
            {
                _etiqueta.AgregarElemento(new ElementoCondicional(elemento, new LambdaCondicion(condicion)));
            }
            return this;
        }

        public EtiquetaBuilder AgregarTextoDividido(string texto, float x, float y, Font fuente, int longitudMaxima, float espaciadoVertical, Brush color = null)
        {
            if (string.IsNullOrEmpty(texto))
                return this;

            var lineas = DividirTexto(texto, longitudMaxima);
            for (int i = 0; i < lineas.Count; i++)
            {
                _etiqueta.AgregarElemento(new ElementoTexto(lineas[i], x, y + (i * espaciadoVertical), fuente, color ?? Brushes.Black));
            }
            return this;
        }

        private List<string> DividirTexto(string texto, int longitudMaxima)
        {
            var lineas = new List<string>();
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

        public EtiquetaBuilder Escalar(float factor)
        {
            if (factor <= 0)
                throw new ArgumentException("El factor de escala debe ser mayor que 0.");
            _etiqueta.Escalar(factor);
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
            return this;
        }
        public Etiqueta Construir()
        {
            return _etiqueta;
        }

        public void Generar(Action<Bitmap> renderAction)
        {
            _etiqueta.Generar(renderAction, _contexto);
        }
    }
}