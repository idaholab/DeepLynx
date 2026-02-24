import * as React from "react";
import { Reorder } from "framer-motion";

interface Props {
  item: string;
}

export const Item = ({ item }: Props) => {

  return (
    <Reorder.Item value={item} id={item}>
      <span>{item}</span>
    </Reorder.Item>
  );
};