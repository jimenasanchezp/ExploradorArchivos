using System;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using D = DocumentFormat.OpenXml.Drawing;

namespace ExploradorArchivos.Services
{
    /// <summary>
    /// Estrategia de conversión que genera una presentación de PowerPoint (.pptx).
    /// </summary>
    public class PptxFileConverter : IFileConverter
    {
        public void Convertir(string rutaOrigen, string rutaDestino, bool esImagen)
        {
            using (PresentationDocument presentacionPowerPoint = PresentationDocument.Create(rutaDestino, PresentationDocumentType.Presentation))
            {
                PresentationPart partePresentacion = presentacionPowerPoint.AddPresentationPart();
                partePresentacion.Presentation = new Presentation();

                SlideMasterPart parteMaestraDiapositiva = partePresentacion.AddNewPart<SlideMasterPart>("rId1");
                parteMaestraDiapositiva.SlideMaster = new SlideMaster(new CommonSlideData(new ShapeTree()));

                SlideLayoutPart parteDiseñoDiapositiva = parteMaestraDiapositiva.AddNewPart<SlideLayoutPart>("rId2");
                parteDiseñoDiapositiva.SlideLayout = new SlideLayout(new CommonSlideData(new ShapeTree()));

                ThemePart parteTemaPresentacion = parteMaestraDiapositiva.AddNewPart<ThemePart>("rId3");
                parteTemaPresentacion.Theme = new D.Theme() { Name = "Office Theme" };

                SlideMasterIdList listaDiapositivasMaestras = new SlideMasterIdList(new SlideMasterId() { Id = (UInt32Value)2147483648U, RelationshipId = "rId1" });
                SlideIdList listaIdDiapositivas = new SlideIdList();

                partePresentacion.Presentation.Append(listaDiapositivasMaestras, listaIdDiapositivas);

                if (!esImagen)
                {
                    int contadorLineasDiapositiva = 0;
                    string textoAcumuladoDiapositiva = "";
                    uint indiceIdDiapositiva = 256;

                    foreach (string lineaTexto in FileConverterService.ExtraerLineas(rutaOrigen))
                    {
                        textoAcumuladoDiapositiva += lineaTexto + "\n";
                        contadorLineasDiapositiva++;

                        if (contadorLineasDiapositiva >= 22)
                        {
                            AgregarDiapositivaAPresentacion(partePresentacion, listaIdDiapositivas, indiceIdDiapositiva, textoAcumuladoDiapositiva, parteDiseñoDiapositiva);
                            indiceIdDiapositiva++;
                            contadorLineasDiapositiva = 0;
                            textoAcumuladoDiapositiva = "";
                        }
                    }

                    if (!string.IsNullOrEmpty(textoAcumuladoDiapositiva))
                    {
                        AgregarDiapositivaAPresentacion(partePresentacion, listaIdDiapositivas, indiceIdDiapositiva, textoAcumuladoDiapositiva, parteDiseñoDiapositiva);
                    }
                }
                else
                {
                    AgregarDiapositivaAPresentacion(partePresentacion, listaIdDiapositivas, 256, "Imagen (soporte de imagen en PPTX no implementado completamente)", parteDiseñoDiapositiva);
                }

                partePresentacion.Presentation.Save();
            }
        }

        private static void AgregarDiapositivaAPresentacion(PresentationPart partePresentacion, SlideIdList listaIdDiapositivas, uint indiceIdDiapositiva, string textoDiapositiva, SlideLayoutPart parteDiseñoDiapositiva)
        {
            SlidePart parteDiapositiva = partePresentacion.AddNewPart<SlidePart>($"rId{indiceIdDiapositiva + 100}");
            parteDiapositiva.Slide = new Slide(new CommonSlideData(new ShapeTree()));

            ShapeTree arbolFormas = parteDiapositiva.Slide.CommonSlideData!.ShapeTree!;

            Shape cajaDeTexto = new Shape();
            cajaDeTexto.NonVisualShapeProperties = new NonVisualShapeProperties(
                new NonVisualDrawingProperties() { Id = 2, Name = "TextBox" },
                new NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties());

            cajaDeTexto.ShapeProperties = new ShapeProperties(
                new D.Transform2D(
                    new D.Offset() { X = 500000L, Y = 500000L },
                    new D.Extents() { Cx = 8000000L, Cy = 6000000L }),
                new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle });

            cajaDeTexto.TextBody = new TextBody(
                new D.BodyProperties(),
                new D.ListStyle(),
                new D.Paragraph(new D.Run(new D.Text(FileConverterService.SanitizarTextoXml(textoDiapositiva)))));

            arbolFormas.AppendChild(cajaDeTexto);
            parteDiapositiva.AddPart(parteDiseñoDiapositiva);

            listaIdDiapositivas.Append(new SlideId() { Id = indiceIdDiapositiva, RelationshipId = partePresentacion.GetIdOfPart(parteDiapositiva) });
        }
    }
}
