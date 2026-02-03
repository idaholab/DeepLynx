// SERVER COMPONENT
import UploadCenterClient from "./UploadCenterClient";
import { ExistingFile } from "../types/types";
import { RecentUpload } from "../types/types";
export const metadata = { title: "Upload Center" };

export default async function Page() {
  const initialAvailableFiles: ExistingFile[] = [];

  return <UploadCenterClient initialAvailableFiles={initialAvailableFiles} />;
}
