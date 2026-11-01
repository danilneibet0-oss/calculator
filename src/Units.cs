using System;

namespace Calculator
{
    /// <summary>A unit as base * Scale + Offset, where base is the category's reference unit
    /// (metre, kilogram, kelvin, ...). The offset is only non-zero for temperatures.</summary>
    public sealed class Unit
    {
        public readonly string Name;
        public readonly string Symbol;
        public readonly double Scale;
        public readonly double Offset;

        public Unit(string name, string symbol, double scale) : this(name, symbol, scale, 0) { }

        public Unit(string name, string symbol, double scale, double offset)
        {
            Name = name;
            Symbol = symbol;
            Scale = scale;
            Offset = offset;
        }

        public override string ToString() { return Name + " (" + Symbol + ")"; }
    }

    public sealed class UnitCategory
    {
        public readonly string Name;
        public readonly Unit[] Units;

        public UnitCategory(string name, params Unit[] units)
        {
            Name = name;
            Units = units;
        }

        public override string ToString() { return Name; }
    }

    public static class Units
    {
        public static readonly UnitCategory[] Categories = new UnitCategory[]
        {
            new UnitCategory("Длина",
                new Unit("Метр", "м", 1), new Unit("Километр", "км", 1000), new Unit("Сантиметр", "см", 0.01),
                new Unit("Миллиметр", "мм", 0.001), new Unit("Дюйм", "in", 0.0254), new Unit("Фут", "ft", 0.3048),
                new Unit("Ярд", "yd", 0.9144), new Unit("Миля", "mi", 1609.344)),
            new UnitCategory("Масса",
                new Unit("Килограмм", "кг", 1), new Unit("Грамм", "г", 0.001), new Unit("Тонна", "т", 1000),
                new Unit("Фунт", "lb", 0.45359237), new Unit("Унция", "oz", 0.028349523125)),
            new UnitCategory("Объём",
                new Unit("Литр", "л", 1), new Unit("Миллилитр", "мл", 0.001), new Unit("Кубометр", "м³", 1000),
                new Unit("Галлон (US)", "gal", 3.785411784)),
            new UnitCategory("Площадь",
                new Unit("Квадратный метр", "м²", 1), new Unit("Квадратный километр", "км²", 1000000),
                new Unit("Гектар", "га", 10000), new Unit("Квадратный сантиметр", "см²", 0.0001),
                new Unit("Квадратный фут", "ft²", 0.09290304), new Unit("Акр", "ac", 4046.8564224)),
            new UnitCategory("Скорость",
                new Unit("Метр в секунду", "м/с", 1), new Unit("Километр в час", "км/ч", 1.0 / 3.6),
                new Unit("Миля в час", "mph", 0.44704), new Unit("Узел", "kn", 0.514444444)),
            new UnitCategory("Время",
                new Unit("Секунда", "с", 1), new Unit("Минута", "мин", 60), new Unit("Час", "ч", 3600),
                new Unit("Сутки", "сут", 86400), new Unit("Неделя", "нед", 604800)),
            new UnitCategory("Данные",
                new Unit("Байт", "Б", 1), new Unit("Килобайт", "КБ", 1024), new Unit("Мегабайт", "МБ", 1024.0 * 1024),
                new Unit("Гигабайт", "ГБ", 1024.0 * 1024 * 1024), new Unit("Терабайт", "ТБ", 1024.0 * 1024 * 1024 * 1024)),
            new UnitCategory("Температура",
                new Unit("Градус Цельсия", "°C", 1, 273.15), new Unit("Градус Фаренгейта", "°F", 5.0 / 9.0, 255.37222222222223),
                new Unit("Кельвин", "K", 1)),
            new UnitCategory("Давление",
                new Unit("Паскаль", "Па", 1), new Unit("Килопаскаль", "кПа", 1000), new Unit("Бар", "бар", 100000),
                new Unit("Атмосфера", "атм", 101325), new Unit("Миллиметр ртутного столба", "мм рт. ст.", 133.322387415),
                new Unit("Фунт на кв. дюйм", "psi", 6894.757293168))
        };

        public static double Convert(double value, Unit from, Unit to)
        {
            double reference = value * from.Scale + from.Offset;
            return (reference - to.Offset) / to.Scale;
        }
    }
}
