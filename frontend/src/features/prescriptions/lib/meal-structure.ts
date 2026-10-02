import type { components } from '@/shared/api/schema';

type Structure = components['schemas']['MealPlanStructureRequest'];

/** Converte o detalhe do servidor no pedido do `PUT`, mantendo os IDs para a reconciliação. */
export function mealStructureFromDetails(
  details: components['schemas']['MealPlanDetailsResponse']
): Structure {
  return {
    meals: details.meals.map((meal) => ({
      id: meal.id,
      meal_type: meal.meal_type,
      order_number: meal.order_number,
      items: meal.items.map((item) => ({
        id: item.id,
        food_id: item.food_id,
        quantity_in_grams: item.quantity_in_grams,
        order_number: item.order_number,
      })),
      supplements: meal.supplements.map((item) => ({
        id: item.id,
        supplement_id: item.supplement_id,
        quantity: item.quantity,
        notes: item.notes,
        order_number: item.order_number,
      })),
    })),
  };
}
