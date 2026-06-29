using System.Collections.Generic;
using Bus_ticket.Models;

namespace Bus_ticket.Helpers;

public static class BusSeatLayoutGenerator
{
    public static List<SeatTemplate> Generate(int totalRows, int totalColumns, int totalFloors, string busType)
    {
        var layout = new List<SeatTemplate>();

        if (totalRows <= 0) totalRows = 1;
        if (totalColumns <= 0) totalColumns = 1;
        if (totalFloors <= 0) totalFloors = 1;

        for (int floor = 1; floor <= totalFloors; floor++)
        {
            string floorPrefix = floor == 1 ? "A" : "B";
            int seatCounter = 1;

            for (int row = 1; row <= totalRows; row++)
            {
                for (int col = 1; col <= totalColumns; col++)
                {
                    string seatNumber = $"{floorPrefix}{seatCounter:D2}";

                    string seatType = busType == "Luxury_Sleeper"
                        ? "Sleeper"
                        : row <= 2 ? "VIP" : "Standard";

                    layout.Add(new SeatTemplate
                    {
                        SeatNumber = seatNumber,
                        Row = row,
                        Column = col,
                        Floor = floor,
                        SeatType = seatType
                    });

                    seatCounter++;
                }
            }
        }

        return layout;
    }
}