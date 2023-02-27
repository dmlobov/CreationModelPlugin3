using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin3
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            Level level1 = GetAddLevel(doc, "Уровень 1");
            Level level2 = GetAddLevel(doc, "Уровень 2");

            AddWall(doc, level1, level2, 10000, 5000);

            return Result.Succeeded;
        }

        public Level GetAddLevel(Document doc, string message)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
               .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();
            Level level = listLevel
                .Where(x => x.Name.Equals(message))
                .FirstOrDefault();
            return level;

        }

        private void AddWall(Document doc, Level level1, Level level2, double width, double depth)
        {
            double widthValue = UnitUtils.ConvertToInternalUnits(width, DisplayUnitType.DUT_MILLIMETERS);
            double depthValue = UnitUtils.ConvertToInternalUnits(depth, DisplayUnitType.DUT_MILLIMETERS);
            double dx = widthValue / 2;
            double dy = depthValue / 2;


            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();

            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);
                walls.Add(wall);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
            }
            AddDoor(doc, level1, walls[0]);


            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);

            transaction.Commit();

            Transaction ts1 = new Transaction(doc, "Установка двери");
            ts1.Start();
            AddDoor(doc, level1, walls[0]);
            ts1.Commit();

            Transaction ts2 = new Transaction(doc, "Установка окон");
            ts2.Start();
            for (int i = 1; i < 4; i++)
            {
                AddWindow(doc, level1, walls[i]);
            }
            ts2.Commit();

            Transaction ts3 = new Transaction(doc, "Создание крыши");
            ts3.Start();
            AddExtrusionRoof(doc, level2, walls, width, depth);
            ts3.Commit();

        }

        private void AddExtrusionRoof(Document doc, Level level2, List<Wall> walls, double width, double depth)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                 .OfClass(typeof(RoofType))
                 .OfType<RoofType>()
                 .Where(x => x.Name.Equals("Типовой - 400мм"))
                 .Where(x => x.FamilyName.Equals("Базовая крыша"))
                 .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            double extrusionStart = -width / 2 - dt;
            double extrusionEnd = width / 2 + dt;

            double curveStart = -depth / 2 - dt;
            double curveEnd = +depth / 2 + dt;

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(0, curveStart, level2.Elevation), new XYZ(0, 0, level2.Elevation + 10)));
            curveArray.Append(Line.CreateBound(new XYZ(0, 0, level2.Elevation + 10), new XYZ(0, curveEnd, level2.Elevation)));

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), doc.ActiveView);
            ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, extrusionStart, extrusionEnd);
            extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;
        }



        //private void AddRoof(Document doc, Level level2, List<Wall> walls)
        //{
        //    RoofType roofType = new FilteredElementCollector(doc)
        //        .OfClass(typeof(RoofType))
        //        .OfType<RoofType>()
        //        .Where(x => x.Name.Equals("Типовой - 400мм"))
        //        .Where(x => x.FamilyName.Equals("Базовая крыша"))
        //        .FirstOrDefault();

        //    double wallWidth = walls[0].Width;
        //    double dt = wallWidth / 2;

        //    List<XYZ> points = new List<XYZ>();
        //    points.Add(new XYZ(-dt, -dt, 0));
        //    points.Add(new XYZ(dt, -dt, 0));
        //    points.Add(new XYZ(dt, dt, 0));
        //    points.Add(new XYZ(-dt, dt, 0));
        //    points.Add(new XYZ(-dt, -dt, 0));

        //    Application application = doc.Application;
        //    CurveArray footprint = application.Create.NewCurveArray();
        //    for (int i = 0; i < 4; i++)
        //    {
        //        LocationCurve curve = walls[i].Location as LocationCurve;
        //        XYZ p1 = curve.Curve.GetEndPoint(0);
        //        XYZ p2 = curve.Curve.GetEndPoint(1);
        //        Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
        //        footprint.Append(line);
        //    }
        //    ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
        //    FootPrintRoof footPrintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);
        //    //ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
        //    //iterator.Reset();
        //    //while (iterator.MoveNext())
        //    //{
        //    //    ModelCurve modelCurve = iterator.Current as ModelCurve;
        //    //    footPrintRoof.set_DefinesSlope(modelCurve, true);
        //    //    footPrintRoof.set_SlopeAngle(modelCurve, 0.5);
        //    //}
        //    foreach (ModelCurve m in footPrintToModelCurveMapping)
        //    {
        //        footPrintRoof.set_DefinesSlope(m, true);
        //        footPrintRoof.set_SlopeAngle(m, 0.5);
        //    }
        //}

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 х 2134 мм"))
                .Where(x => x.FamilyName.Equals("М_Однопольные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
        }

        private void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 1220 мм"))
                .Where(x => x.FamilyName.Equals("М_Неподвижный"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            XYZ centrPoint = GetElementCenter(wall);
            XYZ pointvst = (point + centrPoint) / 2;

            if (!windowType.IsActive)
                windowType.Activate();

            doc.Create.NewFamilyInstance(pointvst, windowType, wall, level1, StructuralType.NonStructural);
        }

        private XYZ GetElementCenter(Element element)
        {
            BoundingBoxXYZ bounding = element.get_BoundingBox(null);
            return (bounding.Max + bounding.Min) / 2;
        }
    }
}
