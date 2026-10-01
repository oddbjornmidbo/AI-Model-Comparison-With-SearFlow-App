import type { Meta, StoryObj } from '@storybook/react-vite'
import { expect, userEvent, within } from 'storybook/test'
import { ConfirmCancelButton } from './ConfirmCancelButton'

const meta = {
  title: 'Components/ConfirmCancelButton',
  component: ConfirmCancelButton,
  args: { subject: 'Ada Lovelace', onConfirm: () => {} },
} satisfies Meta<typeof ConfirmCancelButton>
export default meta
type Story = StoryObj<typeof meta>

export const Idle: Story = {}
export const Disabled: Story = { args: { busy: true } }
export const AwaitingConfirmation: Story = {
  play: async ({ canvasElement }) => {
    const c = within(canvasElement)
    await userEvent.click(c.getByRole('button', { name: /cancel registration/i }))
    await expect(c.getByRole('button', { name: 'Confirm cancel' })).toBeVisible()
  },
}
