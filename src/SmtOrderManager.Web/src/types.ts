// Hand-written counterparts of the API's commands and read models. They only mirror the JSON;
// the business rules live in the API.

export interface Violation {
  target: string;
  message: string;
}

export interface Problem {
  status: number;
  title: string;
  detail?: string;
  violations?: Violation[];
}

export interface CurrentUser {
  username: string;
}

export interface ComponentDetails {
  id: string;
  name: string;
  description: string;
}

export interface CreateComponentCommand {
  name: string;
  description: string | null;
}

export interface UpdateComponentCommand extends CreateComponentCommand {
  id: string;
}

export interface BomEntryDetails {
  componentId: string;
  componentName: string;
  quantity: number;
}

export interface BoardDetails {
  id: string;
  name: string;
  description: string;
  length: number;
  width: number;
  billOfMaterials: BomEntryDetails[];
}

export interface CreateBoardCommand {
  name: string;
  description: string | null;
  length: number;
  width: number;
  billOfMaterials: { componentId: string; quantity: number }[];
}

export interface UpdateBoardCommand extends CreateBoardCommand {
  id: string;
}

export type OrderStatus = 'draft' | 'downloaded';

export interface OrderLineDetails {
  boardId: string;
  boardName: string;
  quantity: number;
}

export interface OrderDetails {
  id: string;
  name: string;
  description: string;
  orderDate: string;
  status: OrderStatus;
  downloadedAt: string | null;
  lines: OrderLineDetails[];
}

export interface CreateOrderCommand {
  name: string;
  description: string | null;
  orderDate: string;
  lines: { boardId: string; quantity: number }[];
}

export interface UpdateOrderCommand extends CreateOrderCommand {
  id: string;
}

export interface OrderDownloadResult {
  orderId: string;
  orderName: string;
  accepted: boolean;
  lineId: string;
  receivedAt: string;
  reasons: string[];
  jobReference: string | null;
}

export interface RemoveResponse {
  removed: number;
}
