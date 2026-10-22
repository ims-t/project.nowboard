using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NowBoard.Data.NowBoard;
using NowBoard.Data.OjpModel;
using NowBoard.Data.Setup;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Serialization;

namespace NowBoard.Components.Pages
{
    public partial class Home : IDisposable
    {
        [Inject]
        public required OpenTransportDataSetup OtdSetup { get; set; }
        [Inject]
        public required NowBoardSetup BoardSetup { get; set; }
        private List<Datalist> datalist = new();
        private readonly CancellationTokenSource _cts = new();

        protected override async Task OnInitializedAsync()
        {
            await DisplayDepartures();

            _ = StartRefreshLoopAsync();
        }
        private async Task StartRefreshLoopAsync()
        {
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            try
            {
                while (await periodicTimer.WaitForNextTickAsync(_cts.Token))
                {
                    await DisplayDepartures();

                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException)
            {
                // Wird ausgelöst, wenn der Benutzer die Seite verlässt – völlig normal
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
                await JS.InvokeVoidAsync("drawClock");
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }


        private async Task DisplayDepartures()
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri("https://api.opentransportdata.swiss/ojp20")
            };
            // Authentifizierung für die API
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", OtdSetup.AuthToken
            );

            var request = new StringContent(RequestCreate(), Encoding.UTF8, "application/xml");
            var response = await client.PostAsync("", request);
            var responseXML = await response.Content.ReadAsStringAsync();

            // XML-Deserialisierung in das OJP-Datenmodell
            var serializer = new XmlSerializer(typeof(Ojp));
            if (serializer.Deserialize(new StringReader(responseXML)) is not Ojp responseOjp)
                return;

            var serviceDelivery = responseOjp.OjpResponse?.ServiceDelivery;
            if (serviceDelivery == null)
                return;

            var allDepartures = new List<DepartureInfo>();

            foreach (var delivery in serviceDelivery.OjpStopEventDeliveryList)
            {
                var stopName = delivery.StopEventResponseContext?.Places?.PlaceList?[0]?.StopPlace?.StopPlaceName?.Text?.Value ?? "Unbekannt";

                if (delivery.StopEventResults == null)
                    continue;

                foreach (var stopEvent in delivery.StopEventResults)
                {
                    var serviceDeparture = stopEvent.StopEvent?.ThisCall?.CallAtStop?.ServiceDeparture;
                    if (serviceDeparture == null)
                        continue;

                    var estimated = DateTime.SpecifyKind(serviceDeparture.EstimatedTime, DateTimeKind.Utc);
                    var scheduled = DateTime.SpecifyKind(serviceDeparture.TimetabledTime, DateTimeKind.Utc);
                    var line = stopEvent.StopEvent?.Service?.PublishedServiceName?.Text?.Value ?? string.Empty;

                    allDepartures.Add(new DepartureInfo
                    {
                        TimetabledTime = scheduled,
                        EstimatedTime = estimated,
                        Line = line,
                        Hinweis = estimated - scheduled, // Berechnet Verspätung
                        Station = stopName
                    });
                }
            }

            var swissZone = TimeZoneInfo.FindSystemTimeZoneById(
                System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                    ? "W. Europe Standard Time"
                    : "Europe/Zurich");

            // Filtern auf relevante Linien und nur die nächsten 5 Abfahrten nehmen
            datalist = allDepartures
                .Where(d => d.Line is "5" or "S21" or "S22")
                .OrderBy(d => d.TimetabledTime)
                .Take(5)
                .Select(d =>
                {
                    var localScheduled = TimeZoneInfo.ConvertTime(d.TimetabledTime, swissZone);
                    var localEstimated = TimeZoneInfo.ConvertTime(d.EstimatedTime, swissZone);

                    return new Datalist
                    {
                        haltestelle = d.Station,
                        timetabledTime = localScheduled.ToString("t"),
                        estimatedTime = localEstimated.ToString("t"),

                        estimatedTime2 = d.EstimatedTime.ToString("o"),

                        linie = d.Line,
                        hinweis = d.Hinweis.TotalMinutes < 0.9 ? string.Empty : $"{(int)Math.Round(d.Hinweis.TotalMinutes)}'"
                    };
                })
                .ToList();
        }

        private int GetLoslaufenCountdown(int startIndex = 0)
        {
            if (datalist == null || datalist.Count <= startIndex) return -999;

            for (int i = startIndex; i < datalist.Count; i++)
            {
                if (DateTime.TryParse(datalist[i].estimatedTime2, out var abfahrtUtc))
                {
                    var abfahrt = abfahrtUtc.ToUniversalTime();
                    var loslaufen = abfahrt.AddMinutes(-BoardSetup.WalkingTimeMinutes);

                    var diff = (loslaufen - DateTime.UtcNow).TotalMinutes;
                    int countdown = (int)Math.Ceiling(diff);

                    if (countdown >= -1)
                    {
                        return countdown;
                    }
                }
            }
            return -999;
        }

        private string RequestCreate()
        {
            var requests = new StringBuilder();

            foreach (var didok in BoardSetup.DiDok)
            {
                requests.AppendLine($"""
            <OJPStopEventRequest>
                <siri:RequestTimestamp>2024-06-01T11:24:34.598Z</siri:RequestTimestamp>
                <siri:MessageIdentifier>SER</siri:MessageIdentifier>

                <Location>
                    <PlaceRef>
                        <StopPlaceRef>{didok}</StopPlaceRef>
                    </PlaceRef>
                </Location>

                <Params>
                    <NumberOfResults>5</NumberOfResults>
                    <StopEventType>departure</StopEventType>
                    <IncludePreviousCalls>false</IncludePreviousCalls>
                    <IncludeOnwardCalls>false</IncludeOnwardCalls>
                    <UseRealtimeData>full</UseRealtimeData>
                </Params>
            </OJPStopEventRequest>
            """);
            }

            return $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <OJP xmlns="http://www.vdv.de/ojp"
             xmlns:siri="http://www.siri.org.uk/siri"
             version="2.0">

            <OJPRequest>
                <siri:ServiceRequest>
                    {requests}
                </siri:ServiceRequest>
            </OJPRequest>

        </OJP>
        """;
        }
    }
}