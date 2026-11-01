using System;
using Calculator;

static class UnitsTests
{
    static int failed;

    static void Check(string name, bool ok)
    {
        if (ok) return;
        failed++;
        Console.WriteLine("FAIL {0}", name);
    }

    static Unit Find(string category, string symbol)
    {
        foreach (UnitCategory c in Units.Categories)
            if (c.Name == category)
                foreach (Unit u in c.Units)
                    if (u.Symbol == symbol) return u;
        throw new ArgumentException(category + "/" + symbol);
    }

    static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9 * Math.Max(1, Math.Abs(b)); }

    public static int Run()
    {
        Check("km to m", Near(Units.Convert(1, Find("Длина", "км"), Find("Длина", "м")), 1000));
        Check("mile to km", Near(Units.Convert(1, Find("Длина", "mi"), Find("Длина", "км")), 1.609344));
        Check("kmh to ms", Near(Units.Convert(36, Find("Скорость", "км/ч"), Find("Скорость", "м/с")), 10));
        Check("hour to minutes", Near(Units.Convert(2, Find("Время", "ч"), Find("Время", "мин")), 120));
        Check("gigabyte to megabytes", Near(Units.Convert(1, Find("Данные", "ГБ"), Find("Данные", "МБ")), 1024));
        Check("atm to kPa", Near(Units.Convert(1, Find("Давление", "атм"), Find("Давление", "кПа")), 101.325));

        Unit c = Find("Температура", "°C"), f = Find("Температура", "°F"), k = Find("Температура", "K");
        Check("0 C is 32 F", Near(Units.Convert(0, c, f), 32));
        Check("100 C is 212 F", Near(Units.Convert(100, c, f), 212));
        Check("-40 F is -40 C", Near(Units.Convert(-40, f, c), -40));
        Check("0 C is 273.15 K", Near(Units.Convert(0, c, k), 273.15));
        Check("0 K is -273.15 C", Near(Units.Convert(0, k, c), -273.15));
        Check("32 F is 0 C", Near(Units.Convert(32, f, c), 0));

        Check("every category has a unit", Units.Categories.Length > 0);
        foreach (UnitCategory cat in Units.Categories)
            Check("category " + cat.Name + " has at least two units", cat.Units.Length >= 2);

        return failed;
    }
}
