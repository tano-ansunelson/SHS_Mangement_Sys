using school_management_system.Models.Enums;

namespace school_management_system.Models.Domain;
    public static class Ui
    {

    public static string Status(ResultStatus s) => s switch
    {
        ResultStatus.Draft => "",
        ResultStatus.Submitted => "info",
        ResultStatus.Returned=> "bad",
        ResultStatus.Reviewed => "warn",
            _ => "ok"

    };

    public static string Outcome(EnrollmentOutcome o) => o switch
    {
        EnrollmentOutcome.Promoted or EnrollmentOutcome.Graduated => "ok",
        EnrollmentOutcome.Repeated => "warn",
        EnrollmentOutcome.Withdrawn => "bad",
            _ => "info"
    };


    public static string Pct(int part, int whole) => whole == 0 ? "0" : Math.Min(100, part * 100 / whole).ToString();

    }

