import { sysAdmin, ORGS, PROJECTS } from "../deeplynx-config";
import { test, expect } from "../fixtures";

test.use({ actingUser: sysAdmin});
test.use({ actingOrg: ORGS.orgA});