// SERVER COMPONENT
import { ExistingFile } from "../types/types";
import UploadCenterClient from "./UploadCenterClient";
export const metadata = { title: "Upload Center" };

export default async function Page() {
  const initialAvailableFiles: ExistingFile[] = [];

  return <UploadCenterClient initialAvailableFiles={initialAvailableFiles} />;
}
