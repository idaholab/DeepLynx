// SERVER COMPONENT
import UploadCenterClient from "./UploadCenterClient";
import { ExistingFile } from "../types/types";
import { RecentUpload } from "../types/types";
export const metadata = { title: "Upload Center" };

export default async function Page() {
  // TODO: replace with real server-side fetches
  const initialAvailableFiles: ExistingFile[] = [
    {
      id: "f1",
      name: "weather_data.csv",
      alias: "Weather Data Record",
      description:
        "Hourly pH sensor readings from groundwater wells in the Teton Valley; baseline hydrochemical survey.",
      lastUpdate: "2025-01-12",
      updatedBy: "Jeren Browning",
      tags: ["Constant Monitoring", "Teton County"],
      dataSource: "https://example.com/source",
      propertiesSources: "None",
      timeSeries: true,
    },
  ];


  return <UploadCenterClient initialAvailableFiles={initialAvailableFiles} />;
}
