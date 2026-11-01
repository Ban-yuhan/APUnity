namespace APProto
{
    public enum Region { None, Daegu, Busan, Seoul }

    public enum WorkerRole { Unloader, Scanner, Inspector, Carrier, Loader }

    // 택배 1개 데이터 (원본 logistics.js 의 { region, isInspected })
    public class Package
    {
        public Region Region = Region.None;   // None = 송장 미스캔
        public bool IsInspected;
        public int Count = 1;   // 파렛트 = 8 (트럭 1칸, 정산은 8개)
        public bool IsPallet => Count > 1;
    }
}
